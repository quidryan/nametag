using System.Diagnostics;
using Xunit;

namespace Tests;

public class PdfGenerationTests
{
    [Fact]
    public void Generate_WithValidInputs_CreatesPdfFile()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_nametag_{Guid.NewGuid()}.pdf");
        var testImagePath = Path.Combine(GetProjectRoot(), "images", "v2_4.png");
        
        try
        {
            // Act
            var exitCode = RunNametagGenerator(
                name: "Test Person",
                team: "Test Team",
                imagePath: testImagePath,
                quote: "Test quote here",
                outputPath: outputPath
            );

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), "Output PDF should exist");
            
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 1024, "PDF should be larger than 1KB");
            
            // Verify it's a valid PDF (starts with %PDF)
            var header = new byte[4];
            using (var fs = File.OpenRead(outputPath))
            {
                fs.Read(header, 0, 4);
            }
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(header));
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Generate_WithLongName_CreatesPdfFile()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), $"test_nametag_{Guid.NewGuid()}.pdf");
        var testImagePath = Path.Combine(GetProjectRoot(), "images", "v2_4.png");
        
        try
        {
            // Act - use a long name that should wrap
            var exitCode = RunNametagGenerator(
                name: "Alexander Hamilton Washington Jefferson",
                team: "Accounts > AuthSec > Auth Usability",
                imagePath: testImagePath,
                quote: "A very long quote that tests the layout",
                outputPath: outputPath
            );

            // Assert
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), "Output PDF should exist");
            
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 1024, "PDF should be larger than 1KB");
        }
        finally
        {
            // Cleanup
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static int RunNametagGenerator(string name, string team, string imagePath, string quote, string outputPath)
    {
        var projectRoot = GetProjectRoot();
        
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectRoot}/jryan-nametag.csproj\" -- " +
                        $"--name \"{name}\" " +
                        $"--team \"{team}\" " +
                        $"--image \"{imagePath}\" " +
                        $"--quote \"{quote}\" " +
                        $"--output \"{outputPath}\"",
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        var stdout = process!.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30000); // 30 second timeout
        
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"STDOUT: {stdout}");
            Console.WriteLine($"STDERR: {stderr}");
        }
        
        return process.ExitCode;
    }

    private static string GetProjectRoot()
    {
        // Navigate up from bin/Debug/net6.0 to find project root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "jryan-nametag.csproj")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not find project root");
    }
}
