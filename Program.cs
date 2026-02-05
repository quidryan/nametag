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
        public void Generate(string name, string team, string imagePath, string quote, string? username, bool borderless, FileStream output);
    }

    public class NametagTemplate : ITemplate
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
        private const int HeaderHeight = 35;
        private const int FooterHeight = 22;
        private const int LineSpacing = 5;
        private const int UsernameFontSize = 12;
        private const int UsernameTopPadding = 0;

        public void Generate(string name, string team, string imagePath, string quote, string? username, bool borderless, FileStream output)
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

            // Calculate content area bounds based on fixed header/footer heights
            var edgeMargin = borderless ? BorderlessMargin : BorderedMargin;
            var contentTop = printTop.Y - HeaderHeight;
            var contentBottom = printBottom.Y + FooterHeight;
            var contentHeight = contentTop - contentBottom;
            
            // Draw team header (white text on blue, centered in header region)
            page.SetTextAndFillColor(255, 255, 255);
            CenteredTextInRegion(page, team, TeamFontSize, cabinFont, contentTop, printTop.Y);
            CenteredTextInRegion(page, quote, QuoteFontSize, cabinFont, printBottom.Y, contentBottom);
            page.DrawRectangle(printBottom.Translate(edgeMargin, FooterHeight),
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

            // Draw username under image if provided (black text, centered under image)
            if (!IsNullOrWhiteSpace(username))
            {
                page.SetTextAndFillColor(0, 0, 0);
                var usernameMeasured = page.MeasureText(username, UsernameFontSize, PdfPoint.Origin, cabinFont);
                var usernameWidth = usernameMeasured.Any() ? usernameMeasured.Max(letter => letter.Location.X) : 0;
                // Center under image, but clamp to not go left of edge margin
                var imageCenterX = imageX + ImageSize / 2;
                var usernameX = Math.Max(edgeMargin, imageCenterX - usernameWidth / 2);
                var usernameY = imageY - UsernameTopPadding - UsernameFontSize;
                page.AddText(username, UsernameFontSize, new PdfPoint(usernameX, usernameY), cabinFont);
                Logger.Info($"Rendered username '{username}' at ({usernameX}, {usernameY})");
            }

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

        /// <summary>
        /// Centers text both horizontally on the page and vertically within a region.
        /// </summary>
        private static void CenteredTextInRegion(PdfPageBuilder page, string text, int fontSize, PdfDocumentBuilder.AddedFont font, double regionBottom, double regionTop)  
        {
            var measureText = page.MeasureText(text, fontSize, PdfPoint.Origin, font);
            var width = measureText.Max(letter => letter.Location.X);
            var indent = (page.PageSize.Width - width) / 2;
            
            // Get the visual bounds of the text
            // GlyphRectangle gives absolute coordinates when measured at Origin
            var glyphTop = measureText.Max(x => x.GlyphRectangle.Top);
            var glyphBottom = measureText.Min(x => x.GlyphRectangle.Bottom);
            var glyphHeight = glyphTop - glyphBottom;
            
            // The baseline was at Y=0 when we measured, so:
            // - glyphTop is the ascender height above baseline
            // - glyphBottom is the descender depth (negative if below baseline)
            
            // To center the visual extent of the text in the region:
            // We want: (glyphBottom + baselineY) and (glyphTop + baselineY) centered in [regionBottom, regionTop]
            // Center of glyph extent = baselineY + (glyphTop + glyphBottom) / 2
            // This should equal: (regionTop + regionBottom) / 2
            var regionCenterY = (regionTop + regionBottom) / 2;
            var glyphCenterOffset = (glyphTop + glyphBottom) / 2;
            var baselineY = regionCenterY - glyphCenterOffset;
            
            Logger.Info($"CenteredTextInRegion: '{text}' fontSize={fontSize}");
            Logger.Info($"  glyphTop={glyphTop:F2}, glyphBottom={glyphBottom:F2}, height={glyphHeight:F2}");
            Logger.Info($"  regionCenter={regionCenterY:F2}, glyphCenterOffset={glyphCenterOffset:F2}, baselineY={baselineY:F2}");
            
            var position = new PdfPoint(indent, baselineY);
            page.AddText(text, fontSize, position, font);
        }

        private static void Background(PdfPageBuilder page, PdfPoint printBottom, double printWidth, double printHeight)
        {
            page.SetTextAndFillColor(100, 126, 221);
            page.DrawRectangle(printBottom, (decimal)printWidth, (decimal)printHeight, 1M, true);
        }

        private static List<string> WrapText(PdfPageBuilder page, string text, int fontSize, PdfDocumentBuilder.AddedFont font, double maxWidth)
        {
            return WrapText(text, maxWidth, testLine =>
            {
                var measured = page.MeasureText(testLine, fontSize, PdfPoint.Origin, font);
                return measured.Any() ? measured.Max(letter => letter.Location.X) : 0;
            });
        }

        /// <summary>
        /// Wraps text into multiple lines based on a maximum width.
        /// </summary>
        /// <param name="text">The text to wrap</param>
        /// <param name="maxWidth">Maximum width for each line</param>
        /// <param name="measureText">Function that measures the width of a string</param>
        /// <returns>List of lines</returns>
        public static List<string> WrapText(string text, double maxWidth, Func<string, double> measureText)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                var width = measureText(testLine);

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
            // Calculate total height and store ascender heights for each line
            double totalHeight = 0;
            var lineMetrics = new List<(double ascender, double descender, double height)>();
            foreach (var line in lines)
            {
                var measured = page.MeasureText(line, fontSize, PdfPoint.Origin, font);
                var ascender = measured.Any() ? measured.Max(x => x.GlyphRectangle.Top) : fontSize;
                var descender = measured.Any() ? Math.Abs(measured.Min(x => x.GlyphRectangle.Bottom)) : 0;
                var height = ascender + descender;
                lineMetrics.Add((ascender, descender, height));
                totalHeight += height;
            }
            totalHeight += (lines.Count - 1) * LineSpacing;

            // Center vertically in content area, but clamp to not exceed contentTop
            var contentHeight = contentTop - contentBottom;
            var startY = totalHeight >= contentHeight 
                ? contentTop  // If text is taller than area, start at top
                : contentTop - (contentHeight - totalHeight) / 2;

            // Render each line centered horizontally in name area
            var currentY = startY;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var measured = page.MeasureText(line, fontSize, PdfPoint.Origin, font);
                var lineWidth = measured.Any() ? measured.Max(letter => letter.Location.X) : 0;
                var lineX = areaLeft + (areaWidth - lineWidth) / 2;
                
                // Subtract only the ascender to position baseline correctly
                currentY -= lineMetrics[i].ascender;
                page.AddText(line, fontSize, new PdfPoint(lineX, currentY), font);
                Logger.Info($"Rendered line '{line}' at ({lineX}, {currentY})");
                // Subtract descender + spacing to get to next line's start
                currentY -= lineMetrics[i].descender + LineSpacing;
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

            [Option('u', "username", Required = false, HelpText = "Optional Roblox display name, shown under the image")]
            public string? Username { get; set; }
            
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
                    
                    var generator = new NametagTemplate();
                    generator.Generate(o.Name, o.Team, o.Image, o.Quote, o.Username, o.Borderless, output);
                });
        }
    }
}