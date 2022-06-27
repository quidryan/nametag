using System.CodeDom.Compiler;
using System.Diagnostics;
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
        public void Generate(Dictionary<string, string> fields, bool borderless, FileStream output);
    }

    class FigmaTemplate : ITemplate
    {
        public void Generate(Dictionary<string, string> fields, bool borderless, FileStream output)
        {
            var team = fields["Team"];
            var fullName = fields["FullName"];
            string tagline = fields.ContainsKey("TagLine") ? fields["TagLine"] : " ";

            if (output == null || IsNullOrWhiteSpace(fullName) || IsNullOrWhiteSpace(team))
            {
                throw new ArgumentNullException("Output was null");
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

            page.SetTextAndFillColor(255, 255, 255);
            var teamHeight = CenteredText(page, team, 24, cabinFont, (indent, height) => printTop.Translate(indent, -1 * (height+3)));
            var tagLineHeight = CenteredText(page, tagline, 12, cabinFont, (indent, height) => printBottom.Translate(indent, height));
            
            var edgeMargin = borderless ? 12 : 42;
            page.DrawRectangle(printBottom.Translate(edgeMargin, tagLineHeight + 14),
                (decimal) (printWidth-edgeMargin*2),
                (decimal) (printHeight - teamHeight - tagLineHeight - 27), 
                1M, 
                true);
            // TBD Drop shadow

            page.SetTextAndFillColor(0,0,0);
            var fullNameHeight = CenteredText(page, fullName, 50, futuraFont, (indent, height) => printTop.Translate(indent, -1 * (teamHeight + 17 + height)));

            var hotpotIndent = 150;
            var imageDim = 50;
            var imageY = 30;
            var hotpotPlacement = new PdfRectangle(printBottom.Translate(hotpotIndent, imageY), new PdfPoint(hotpotIndent+imageDim, printBottom.Y+imageY+imageDim));
            page.AddPng(File.ReadAllBytes("images/v2_4.png"), hotpotPlacement);

            var scoreDim = 25;
            var scoreY = imageY + 10;
            var chiliPng = File.ReadAllBytes("images/v2_10.png");
            var hotpotScore = int.Parse(fields.ContainsKey("HotPot") ? fields["HotPot"] : "3");
            var chiliIndent = hotpotIndent + imageDim;
            for (int i = 0; i < hotpotScore; i++)
            {
                var chiliPlacement = new PdfRectangle(printBottom.Translate(chiliIndent, scoreY), new PdfPoint(chiliIndent+scoreDim, printBottom.Y+scoreY+scoreDim));
                page.AddPng(chiliPng, chiliPlacement);
                chiliIndent += scoreDim + 3;
            }
            
            var bobaIndent = printWidth / 2 + 20;
            var bobaPlacement = new PdfRectangle(printBottom.Translate(bobaIndent, imageY), new PdfPoint(bobaIndent+imageDim, printBottom.Y+imageY+imageDim));
            page.AddPng(File.ReadAllBytes("images/v2_14.png"), bobaPlacement);

            var icePng = File.ReadAllBytes("images/v2_16.png");
            var bobaScore = int.Parse(fields.ContainsKey("Boba") ? fields["Boba"] : "3");
            var iceIndent = bobaIndent + imageDim;
            for (int i = 0; i < bobaScore; i++)
            {
                var icePlacement = new PdfRectangle(printBottom.Translate(iceIndent, scoreY), new PdfPoint(iceIndent+scoreDim, printBottom.Y+scoreY+scoreDim));
                page.AddPng(icePng, icePlacement);
                iceIndent += scoreDim + 3;
            }

            var fileBytes = builder.Build();

            try
            {
                using (output)
                {
                    output.Write(fileBytes, 0, fileBytes.Length);
                    Logger.Info(($"File output to: {output.Name}"));
                    
                }
            }
            catch (Exception ex)
            {
                Logger.Info($"Failed to write output to file due to error: {ex}.");
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
    }
 
    class Program
    {

        public class GenerateOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; } = false;

            [Option('b', "borderless", Required = false, HelpText = "Print an ideal borderless layout, only works with inkjet printers")]
            public bool Borderless { get; set; } = false;

            [Option(shortName:'t', longName:"templateData", Required = true, HelpText = "Template fields pairs (FullName, Team, TagLine), e.g. Team=Account Identity")]
            public IEnumerable<string> TemplateFields { get; set; }
            
            [Option('o', "output", Required = false, HelpText = "File to save output to")]
            public string? Output {get; set;}
        }

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<GenerateOptions>(args)
                .WithParsed<GenerateOptions>( o =>
                {
                    var location = Path.Combine(Environment.CurrentDirectory, o.Output ?? "result.pdf");
                    var output = File.Create(location);
                    var fieldDictionary = o.TemplateFields.Select(t => t.Split('=')).ToDictionary(t => t[0], t => t[1]);

                    Logger.Verbose = o.Verbose;
                    
                    var generator = new FigmaTemplate();
                    generator.Generate(fieldDictionary, o.Borderless, output);
                });
        }
    }
}