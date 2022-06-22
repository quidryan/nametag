using CommandLine;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Kernel.Geom;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Pdf.Canvas;
using iText.IO.Image;
using iText.Layout.Borders;
using iText.Layout.Renderer;

// See https://aka.ms/new-console-template for more information

namespace Halfempty.Nametag
{

    class Generator
    {

        private string FullName;
        private string Title;
        
        private FileStream Output;

        public Generator(string fullName, string title, FileStream output)
        {
            FullName = fullName;
            Title = title;
            Output = output;
        }

        public void Generate()
        {
            if(Output == null || String.IsNullOrWhiteSpace(FullName) || String.IsNullOrWhiteSpace(Title))
            {
                throw new ArgumentNullException("Output was null");
            }

            var writer = new PdfWriter(Output);
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
            
            Paragraph nameParagraph = new Paragraph(FullName)
                .SetMargin(30)
                .SetMultipliedLeading(1)
                .SetFontSize(fontSize)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMaxWidth(forText);

            Paragraph titleParagraph = new Paragraph(Title)
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

            [Option('n', "name", Required = true, HelpText = "Full name")]
            public string? FullName {get; set;}

            [Option('t', "title", Required = true, HelpText = "Title or Team")]
            public string? Title {get; set;}

            [Option('a', "avatar", Required = false, HelpText = "Avatar name")]
            public string? AvatarName {get; set;}

            [Option('o', "output", Required = false, HelpText = "File to save output to")]
            public string? Output {get; set;}            
        }

        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<GenerateOptions>(args)
                .WithParsed<GenerateOptions>( o =>
                {
                    FileStream output = new FileStream(o.Output == null ? "result.pdf" : o.Output, FileMode.Create);
                    var generator = new Generator(o.FullName!, o.Title!, output);
                    generator.Generate();
                });
        }
    }
}