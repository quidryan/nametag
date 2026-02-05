using CommandLine;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using static System.String;

// See https://aka.ms/new-console-template for more information

namespace Halfempty.Nametag
{
    static class Logger
    {
        public static bool Verbose { get; set; }

        public static void Info(string msg)
        {
            if (Verbose)
            {
                Console.WriteLine(msg);
            }
        }
    }

    interface ITemplate
    {
        public void Generate(string name, string team, string imagePath, string quote, bool borderless, FileStream output);
    }

    class FigmaTemplate : ITemplate
    {
        // Layout constants
        private const int TeamFontSize = 24;
        private const int NameFontSize = 50;
        private const int QuoteFontSize = 12;
        private const int ImageSize = 80;
        private const int ContentMargin = 40;
        private const int ImagePadding = 20;
        private const int BorderlessMargin = 12;
        private const int BorderedMargin = 42;
        private const int HeaderPadding = 3;
        private const int ContentTopPadding = 17;
        private const int ContentBottomPadding = 14;
        private const int LineSpacing = 5;

        public void Generate(string name, string team, string imagePath, string quote, bool borderless, FileStream output)
        {
            if (output == null || IsNullOrWhiteSpace(name) || IsNullOrWhiteSpace(team) || IsNullOrWhiteSpace(imagePath) || IsNullOrWhiteSpace(quote))
            {
                throw new ArgumentException("All parameters (name, team, imagePath, quote) are required");
            }

            var builder = new PdfDocumentBuilder();
            var cabinFont = builder.AddTrueTypeFont(File.ReadAllBytes("fonts/Cabin-Bold.ttf"));
            var futuraFont = builder.AddTrueTypeFont(File.ReadAllBytes("fonts/Futura-Regular.ttf"));

            var page = builder.AddPage(PageSize.Letter);
            var printHeight = page.PageSize.Height / 4;
            var printWidth = page.PageSize.Width;
            var printTop = borderless ? new PdfPoint(0, printHeight) : new PdfPoint(0, printHeight*2);
            var printBottom = borderless ? PdfPoint.Origin : new PdfPoint(0, printHeight);

            Background(page, printBottom, printWidth, printHeight);

            // Draw team header (white text on blue)
            page.SetTextAndFillColor(255, 255, 255);
            var teamHeight = CenteredText(page, team, TeamFontSize, cabinFont, (indent, height) => printTop.Translate(indent, -1 * (height + HeaderPadding)));
            var quoteHeight = CenteredText(page, quote, QuoteFontSize, cabinFont, (indent, height) => printBottom.Translate(indent, height));
            
            // Draw white content area
            var edgeMargin = borderless ? BorderlessMargin : BorderedMargin;
            var contentTop = printTop.Y - teamHeight - ContentTopPadding;
            var contentBottom = printBottom.Y + quoteHeight + ContentBottomPadding;
            var contentHeight = contentTop - contentBottom;
            page.DrawRectangle(printBottom.Translate(edgeMargin, quoteHeight + ContentBottomPadding),
                (decimal)(printWidth - edgeMargin * 2),
                (decimal)contentHeight, 
                1M, 
                true);

            // Draw personalized image (left side of content area)
            var imageBytes = File.ReadAllBytes(imagePath);
            var imageX = edgeMargin + ContentMargin;
            var imageY = contentBottom + (contentHeight - ImageSize) / 2;
            var imagePlacement = new PdfRectangle(
                new PdfPoint(imageX, imageY),
                new PdfPoint(imageX + ImageSize, imageY + ImageSize));
            page.AddPng(imageBytes, imagePlacement);

            // Calculate available width for name (to the right of the image)
            var nameAreaLeft = imageX + ImageSize + ImagePadding;
            var nameAreaRight = printWidth - edgeMargin - ContentMargin;
            var nameAreaWidth = nameAreaRight - nameAreaLeft;

            // Draw name with text wrapping (black text)
            page.SetTextAndFillColor(0, 0, 0);
            var nameLines = WrapText(page, name, NameFontSize, futuraFont, nameAreaWidth);
            var totalNameHeight = RenderWrappedText(page, nameLines, NameFontSize, futuraFont, nameAreaLeft, nameAreaWidth, contentTop, contentBottom);

            var fileBytes = builder.Build();

            using (output)
            {
                output.Write(fileBytes, 0, fileBytes.Length);
                Logger.Info($"File output to: {output.Name}");
            }

        }

        private static double CenteredText(PdfPageBuilder page, string text, int fontSize, PdfDocumentBuilder.AddedFont font, Func<double, double, PdfPoint> positioning)  
        {
            var measureText = page.MeasureText(text, fontSize, PdfPoint.Origin, font);
            var width = measureText.Max(letter => letter.Location.X);
            
            var indent = (page.PageSize.Width - width) / 2;
            var topOfText = measureText.Max(x => x.GlyphRectangle.Top);
            var bottomOfText = measureText.Min(x => x.GlyphRectangle.Bottom);
            var height = topOfText - bottomOfText;
            var position = positioning(indent, height);
            Logger.Info($"Positioning: {position}");
            page.AddText(text, fontSize, position, font);
            return height;
        }

        private static void Background(PdfPageBuilder page, PdfPoint printBottom, double printWidth, double printHeight)
        {
            page.SetTextAndFillColor(100, 126, 221);
            page.DrawRectangle(printBottom, (decimal)printWidth, (decimal)printHeight, 1M, true);
        }

        private static List<string> WrapText(PdfPageBuilder page, string text, int fontSize, PdfDocumentBuilder.AddedFont font, double maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                var measured = page.MeasureText(testLine, fontSize, PdfPoint.Origin, font);
                var width = measured.Any() ? measured.Max(letter => letter.Location.X) : 0;

                if (width > maxWidth && currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (currentLine.Length > 0) lines.Add(currentLine);
            return lines;
        }

        private static double RenderWrappedText(PdfPageBuilder page, List<string> lines, int fontSize, PdfDocumentBuilder.AddedFont font, double areaLeft, double areaWidth, double contentTop, double contentBottom)
        {
            // Calculate total height of all lines
            double totalHeight = 0;
            var lineHeights = new List<double>();
            foreach (var line in lines)
            {
                var measured = page.MeasureText(line, fontSize, PdfPoint.Origin, font);
                var top = measured.Any() ? measured.Max(x => x.GlyphRectangle.Top) : fontSize;
                var bottom = measured.Any() ? measured.Min(x => x.GlyphRectangle.Bottom) : 0;
                var lineHeight = top - bottom;
                lineHeights.Add(lineHeight);
                totalHeight += lineHeight;
            }
            totalHeight += (lines.Count - 1) * LineSpacing;

            // Center vertically in content area
            var contentHeight = contentTop - contentBottom;
            var startY = contentTop - (contentHeight - totalHeight) / 2;

            // Render each line centered horizontally in name area
            var currentY = startY;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var measured = page.MeasureText(line, fontSize, PdfPoint.Origin, font);
                var lineWidth = measured.Any() ? measured.Max(letter => letter.Location.X) : 0;
                var lineX = areaLeft + (areaWidth - lineWidth) / 2;
                
                currentY -= lineHeights[i];
                page.AddText(line, fontSize, new PdfPoint(lineX, currentY), font);
                Logger.Info($"Rendered line '{line}' at ({lineX}, {currentY})");
                currentY -= LineSpacing;
            }

            return totalHeight;
        }
    }
 
    class Program
    {

        public class GenerateOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; } = false;

            [Option('b', "borderless", Required = false, HelpText = "Print an ideal borderless layout, only works with inkjet printers")]
            public bool Borderless { get; set; } = false;

            [Option('n', "name", Required = true, HelpText = "The person's full name")]
            public string Name { get; set; } = "";

            [Option('t', "team", Required = true, HelpText = "Team name string")]
            public string Team { get; set; } = "";

            [Option('i', "image", Required = true, HelpText = "Path to personalized PNG image")]
            public string Image { get; set; } = "";

            [Option('q', "quote", Required = true, HelpText = "Funny quote for the bottom")]
            public string Quote { get; set; } = "";
            
            [Option('o', "output", Required = false, HelpText = "File to save output to")]
            public string? Output { get; set; }
        }

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<GenerateOptions>(args)
                .WithParsed<GenerateOptions>(o =>
                {
                    var location = Path.Combine(Environment.CurrentDirectory, o.Output ?? "result.pdf");

                    Logger.Verbose = o.Verbose;
                    var output = File.Create(location);
                    
                    var generator = new FigmaTemplate();
                    generator.Generate(o.Name, o.Team, o.Image, o.Quote, o.Borderless, output);
                });
        }
    }
}