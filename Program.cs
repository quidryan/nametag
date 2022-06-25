using System.Diagnostics;
using CommandLine;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Kernel.Geom;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Pdf.Canvas;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using static System.String;

// See https://aka.ms/new-console-template for more information

namespace Halfempty.Nametag
{

    interface ITemplate
    {
        public void Generate(Dictionary<string, string> fields, FileStream output);
    }

    class FigmaTemplate : ITemplate
    {
        public void Generate(Dictionary<string, string> fields, FileStream output)
        {
            var team = fields["Team"];
            var fullName = fields["FullName"];
            string? tagline;
            fields.TryGetValue("TagLine", out tagline);
            
            if(output == null || IsNullOrWhiteSpace(fullName) || IsNullOrWhiteSpace(team))
            {
                throw new ArgumentNullException("Output was null");
            }

            var background = new DeviceRgb(100, 126, 221);
            
            var writer = new PdfWriter(output);
            var pdfDocument = new PdfDocument(writer);
            var ps = PageSize.LETTER;
            var page = pdfDocument.AddNewPage(ps);
            var canvas = new PdfCanvas(page);

            Console.WriteLine("Generating file...");

            float fontSize = 40;
            var height = ps.GetHeight() / 4;
            var forImage = ps.GetWidth() / 4;
            var forText = ps.GetWidth()-forImage;
            
            Paragraph teamParagraph = new Paragraph(team)
                .SetMargin(1)
                .SetMultipliedLeading(1)
                .SetMarginTop(10)
                .SetMarginBottom(10)
                .SetFontSize(16)
                .SetTextAlignment(TextAlignment.CENTER)
                //.SetFontFamily("Cabin")
                .SetFontColor(DeviceRgb.WHITE)
                .SetMaxWidth(ps.GetWidth());

            Paragraph fullNameParagraph = new Paragraph(fullName)
                .SetMargin(10)
                .SetMultipliedLeading(1)
                .SetFontSize( (int) (fontSize*.7))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMaxWidth(forText);
            
            var rect = new Rectangle(0, 0, ps.GetWidth(), height);

            // Background coloring
            canvas.SaveState();
            canvas.SetFillColor(background);
            canvas.Rectangle(0, 0, ps.GetWidth(), height);
            canvas.Fill();
            canvas.SetFillColor(DeviceRgb.WHITE);
            var innerBorder = 15;
            var innerBorderTop = 40;
            canvas.Rectangle(innerBorder, innerBorderTop, ps.GetWidth()-innerBorder*2, height - innerBorderTop*2);
            canvas.Fill();
            canvas.RestoreState();
            
            new Canvas(canvas, rect)
                .Add(teamParagraph);
            
            canvas.Rectangle(rect);
            
            pdfDocument.Close();

        }
    }
    class Generator : ITemplate
    {
        public void Generate(Dictionary<string, string> fields, FileStream output)
        {
            var fullName = fields["FullName"];
            var title = fields["Title"];
                
            if(output == null || IsNullOrWhiteSpace(fullName) || IsNullOrWhiteSpace(title))
            {
                throw new ArgumentNullException("Output was null");
            }

            var writer = new PdfWriter(output);
            var pdfDocument = new PdfDocument(writer);
            var ps = PageSize.LETTER;
            var page = pdfDocument.AddNewPage(ps);
            var canvas = new PdfCanvas(page);

            Console.WriteLine("Generating file...");

            float fontSize = 40;
            var height = ps.GetHeight() / 4;
            var forImage = ps.GetWidth() / 4;
            var forText = ps.GetWidth()-forImage;

            var userId = 3622925419;
            var avatar =
                ImageDataFactory.Create(new Uri(
                    $"https://www.roblox.com/headshot-thumbnail/image?userId={userId}&width=150&height=150&format=png"));
            canvas.AddImageAt(avatar, 40, 40, true);
            //canvas.AddImageWithTransformationMatrix(avatar, -1, 0, 0, -1, 15, 15);
            
            Paragraph nameParagraph = new Paragraph(fullName)
                .SetMargin(30)
                .SetMultipliedLeading(1)
                .SetFontSize(fontSize)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMaxWidth(forText);

            Paragraph titleParagraph = new Paragraph(title)
                .SetMargin(10)
                .SetMultipliedLeading(1)
                .SetFontSize( (int) (fontSize*.7))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMaxWidth(forText);
            
            var rect = new Rectangle(forImage, 0, forText, height);
            new Canvas(canvas, rect)
                .Add(nameParagraph)
                .Add(titleParagraph)
                .SetBorder(Border.NO_BORDER);
            
            canvas.Rectangle(rect);
            
            nameParagraph.SetRotationAngle(Math.PI);
            titleParagraph.SetRotationAngle(Math.PI);
            var rect2 = new Rectangle(0, height, forText, height);
            new Canvas(canvas, rect2)
                .Add(titleParagraph)
                .Add(nameParagraph)
                .SetBorder(Border.NO_BORDER);
            canvas.Rectangle(rect);
            
            pdfDocument.Close();

        }
    }
    class Program
    {

        public class GenerateOptions
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose {get; set;}

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
                    FileStream output = new FileStream(o.Output ?? "result.pdf", FileMode.Create);
                    var fieldDictionary = o.TemplateFields.Select(t => t.Split('=')).ToDictionary(t => t[0], t => t[1]);
                    var generator = new FigmaTemplate();
                    generator.Generate(fieldDictionary, output);
                });
        }
    }
}