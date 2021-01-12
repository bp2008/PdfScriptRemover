using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PdfScriptRemover
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				if (args.Length != 2)
				{
					Console.Error.WriteLine("PdfScriptRemover " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
					Console.Error.WriteLine(" - powered by iText7 for .NET - ");
					Console.Error.WriteLine(" - licensed by GNU AFFERO GENERAL PUBLIC LICENSE - ");
					Console.Error.WriteLine();
					Console.Error.WriteLine(
						"This program reads a PDF file and removes embedded JavaScript " + Environment.NewLine
						+ "  and embedded/attached files.  The output file is only " + Environment.NewLine
						+ "  written if something is removed.");
					Console.Error.WriteLine();
					Console.Error.WriteLine("Usage: PdfScriptRemover.exe inputFile outputFile");
					return 0;
				}

				// Validate inputs
				FileInfo inputFile = new FileInfo(args[0]);
				if (!inputFile.Exists)
				{
					DirectoryInfo diInputDir = new DirectoryInfo(args[0]);
					if (diInputDir.Exists)
					{
						if (Directory.Exists(args[1]))
						{
							int retVal = 0;
							foreach (FileInfo fi in diInputDir.GetFiles("*.pdf"))
							{
								int retValInner = Main(new string[] { fi.FullName.ToString(), args[1] });
								if (retValInner != 0)
									retVal = retValInner;
							}
							return retVal;
						}
						else
						{
							Console.Error.WriteLine("Input path was a folder, but output path was not a folder");
							return 1;
						}
					}
					Console.Error.WriteLine("Input file does not exist: " + args[0]);
					return 1;
				}

				FileInfo outputFile;
				if (Directory.Exists(args[1]))
					outputFile = new FileInfo(Path.Combine(new DirectoryInfo(args[1]).FullName, inputFile.Name));
				else
					outputFile = new FileInfo(args[1]);

				if (outputFile.Exists)
				{
					Console.Error.WriteLine("Output file already exists: " + args[1]);
					return 1;
				}

				if (inputFile.FullName.Equals(outputFile.FullName, StringComparison.OrdinalIgnoreCase))
				{
					Console.Error.WriteLine("Input and output file is not allowed to be the same file: " + args[0]);
					return 1;
				}

				PdfCleaner cleaner = new PdfCleaner(inputFile.FullName, outputFile.FullName, str => Console.Write(str));
				cleaner.Clean();

				return 0;
			}
			catch (Exception ex)
			{
				Console.Error.Write(ex.ToString());
				return 1;
			}
			finally
			{
				if (System.Diagnostics.Debugger.IsAttached)
				{
					Console.WriteLine();
					Console.WriteLine("Press Enter to exit");
					Console.ReadLine();
				}
			}
		}
	}
}
