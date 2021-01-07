using iText.Kernel.Pdf;
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
		public static readonly Encoding Utf8NoBOM = new UTF8Encoding(false);

		private static List<string> removedItems = new List<string>();
		private static HashSet<PdfDictionary> traversed = new HashSet<PdfDictionary>();

		static int Main(string[] args)
		{
			try
			{
				if (args.Length != 2)
				{
					Console.WriteLine("PdfScriptRemover " + Assembly.GetExecutingAssembly().GetName().Version.ToString());
					Console.WriteLine(" - powered by iText7 for .NET - ");
					Console.WriteLine(" - licensed by GNU AFFERO GENERAL PUBLIC LICENSE - ");
					Console.WriteLine();
					Console.WriteLine("This program removes embedded JavaScript from a PDF file. ");
					Console.WriteLine();
					Console.WriteLine("Usage: PdfScriptRemover.exe inputFile outputFile");
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
							int retVal=0;
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

				// Do the cleanup
				using (PdfReader reader = new PdfReader(inputFile.FullName))
				using (PdfWriter writer = new PdfWriter(outputFile.FullName))
				{
					// Ignore permissions set by document author
					reader.SetUnethicalReading(true);

					using (PdfDocument pdfDoc = new PdfDocument(reader, writer))
					{
						// Clean "Trailer"
						CleanDictionary("Trailer", pdfDoc.GetTrailer());

						// Clean "Outlines"
						if (pdfDoc.HasOutlines())
						{
							PdfOutline outline = pdfDoc.GetOutlines(true);
							CleanOutline(outline);
						}

						// Clean "Catalog" (document-level scripts)
						PdfCatalog pdfCat = pdfDoc.GetCatalog();
						PdfDictionary d = pdfCat.GetPdfObject().GetAsDictionary(PdfName.Names);
						CleanDictionary("Catalog/Names", d);

						// Clean pages
						int pageCount = pdfDoc.GetNumberOfPages();
						for (int i = 1; i <= pageCount; i++)
						{
							PdfPage page = pdfDoc.GetPage(i);
							PdfDictionary pageDict = page?.GetPdfObject();
							CleanDictionary("Page " + i, pageDict);
							CleanArray("Page " + i + " Kids", pageDict?.GetAsArray(PdfName.Kids));
						}
					}
				}


				// Log and report what was removed
				StringBuilder sb = new StringBuilder();
				if (removedItems.Count == 0)
				{
					sb.AppendLine("Removed " + removedItems.Count + " items.");
				}
				else
				{
					sb.AppendLine("Removed " + removedItems.Count + " items:");
					foreach (string item in removedItems)
						sb.AppendLine(item);
				}
				WriteLog(inputFile.FullName, outputFile.FullName, sb.ToString());
				return 0;
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
		#region PDF Manipulation
		private static void CleanOutline(PdfOutline outline)
		{
			if (outline == null)
				return;
			CleanDictionary("Outline " + outline.GetTitle(), outline.GetContent());
			IList<PdfOutline> children = outline.GetAllChildren();
			for (int i = 0; i < children.Count; i++)
				CleanOutline(children[i]);
		}

		/// <summary>
		/// Removes unwanted items from a PdfDictionary.
		/// </summary>
		/// <param name="d">dictionary</param>
		public static void CleanDictionary(string dictionaryLabel, PdfDictionary d)
		{
			if (d == null)
				return;
			if (traversed.Contains(d))
				return;
			traversed.Add(d);

			RemoveDictionaryItem(dictionaryLabel, d, PdfName.JavaScript);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.JS);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.AA);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.OpenAction);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.FileAttachment);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.EmbeddedFile);
			RemoveDictionaryItem(dictionaryLabel, d, PdfName.EmbeddedFiles);

			PdfDictionary action = d.GetAsDictionary(PdfName.A);
			if (action != null)
			{
				if (PdfName.Launch.Equals(action.GetAsName(PdfName.S)))
				{
					RemoveDictionaryItem(dictionaryLabel + " > A", action, PdfName.F);
					RemoveDictionaryItem(dictionaryLabel + " > A", action, PdfName.Win);
				}
				// The above was based on https://kb.itextpdf.com/home/it5kb/examples/itext-in-action-chapter-13-pdfs-inside-out#iTextinActionChapter13:PDFsinside-out-removelaunchactions
				// But it seems like we should just remove this action altogether.
				RemoveDictionaryItem(dictionaryLabel, d, PdfName.A);
			}

			foreach (PdfName key in d.KeySet())
			{
				CleanObject(dictionaryLabel + " > " + key.ToString(), d.Get(key));
			}
		}

		/// <summary>
		/// Removes unwanted items from a PdfArray.
		/// </summary>
		/// <param name="a">array</param>
		public static void CleanArray(string arrayLabel, PdfArray a)
		{
			if (a == null)
				return;
			for (int i = 0; i < a.Size(); i++)
			{
				CleanObject(arrayLabel + "[" + i + "]", a.Get(i));
			}
		}

		private static void CleanObject(string objectLabel, PdfObject obj)
		{
			if (obj == null)
				return;

			if (obj is PdfIndirectReference)
				obj = (obj as PdfIndirectReference).GetRefersTo(true);

			if (obj is PdfDictionary)
				CleanDictionary(objectLabel, obj as PdfDictionary);
			else if (obj is PdfArray)
				CleanArray(objectLabel, obj as PdfArray);
		}

		/// <summary>
		/// Removes an item from a PdfDictionary, logging its removal in the removedItems list.
		/// </summary>
		/// <param name="d">dictionary</param>
		/// <param name="key">key to remove</param>
		public static void RemoveDictionaryItem(string dictionaryLabel, PdfDictionary d, PdfName key)
		{
			if (d == null)
				return;
			if (d.ContainsKey(key))
			{
				PdfObject removed = d.Remove(key);
				if (removed == null)
					removedItems.Add("(" + dictionaryLabel + ") " + key.ToString() + ": null");
				else
					removedItems.Add("(" + dictionaryLabel + ") " + key.ToString() + ": " + removed.ToString());
			}
		}
#endregion
		#region Misc
		private static void WriteLog(string input, string output, string text)
		{
			Console.Write(text);

			using (FileStream fs = new FileStream("log.txt", FileMode.Append, FileAccess.Write, FileShare.Read))
			using (StreamWriter sw = new StreamWriter(fs, Utf8NoBOM))
			{
				sw.WriteLine();
				sw.WriteLine(DateTime.Now.ToString() + ": " + input + " > " + output);
				sw.WriteLine();
				sw.WriteLine(text);
			}
		}
		#endregion
	}
}
