using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PdfScriptRemover
{
	public class PdfCleaner
	{
		HashSet<PdfDictionary> traversed = new HashSet<PdfDictionary>();
		List<string> removedItems = new List<string>();
		bool cleaned = false;
		string inputFile;
		string outputFile;
		Action<string> cleanedItemCallback;

		public PdfCleaner(string inputFile, string outputFile, Action<string> cleanedItemCallback)
		{
			this.inputFile = inputFile;
			this.outputFile = outputFile;
			this.cleanedItemCallback = cleanedItemCallback;
		}

		public void Clean()
		{
			if (cleaned)
				throw new Exception("Unable to reuse PdfCleaner instance");
			cleaned = true;

			// Do the cleanup
			byte[] inputData = File.ReadAllBytes(inputFile);
			using (MemoryStream msIn = new MemoryStream(inputData))
			using (PdfReader reader = new PdfReader(msIn))
			using (MemoryStream msOut = new MemoryStream())
			{
				using (PdfWriter writer = new PdfWriter(msOut))
				{
					writer.SetCloseStream(false);

					// Ignore permissions set by document author
					reader.SetUnethicalReading(true);

					using (PdfDocument pdfDoc = new PdfDocument(reader, writer))
					{
						// Clean pages
						int pageCount = pdfDoc.GetNumberOfPages();
						for (int i = 1; i <= pageCount; i++)
						{
							PdfPage page = pdfDoc.GetPage(i);
							PdfDictionary pageDict = page?.GetPdfObject();
							CleanDictionary("Page " + i, pageDict);
							CleanArray("Page " + i + " Kids", pageDict?.GetAsArray(PdfName.Kids));
						}

						// Clean "Catalog" (document-level scripts)
						PdfCatalog pdfCat = pdfDoc.GetCatalog();
						PdfDictionary d = pdfCat.GetPdfObject();
						CleanDictionary("Catalog", d);


						// Clean "Outlines"
						if (pdfDoc.HasOutlines())
						{
							PdfOutline outline = pdfDoc.GetOutlines(true);
							CleanOutline(outline);
						}

						// Clean "Trailer"
						CleanDictionary("Trailer", pdfDoc.GetTrailer());
					}
				}

				if (removedItems.Count > 0)
				{
					using (FileStream fsOut = File.Create(outputFile))
						msOut.WriteTo(fsOut);
				}
			}
		}

		private void CleanOutline(PdfOutline outline)
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
		private void CleanDictionary(string dictionaryLabel, PdfDictionary d)
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
					// The above was based on https://kb.itextpdf.com/home/it5kb/examples/itext-in-action-chapter-13-pdfs-inside-out#iTextinActionChapter13:PDFsinside-out-removelaunchactions
					// But it seems like we should just remove this action altogether.
					RemoveDictionaryItem(dictionaryLabel, d, PdfName.A);
				}
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
		private void CleanArray(string arrayLabel, PdfArray a)
		{
			if (a == null)
				return;
			for (int i = 0; i < a.Size(); i++)
			{
				CleanObject(arrayLabel + "[" + i + "]", a.Get(i));
			}
		}

		private void CleanObject(string objectLabel, PdfObject obj)
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
		private void RemoveDictionaryItem(string dictionaryLabel, PdfDictionary d, PdfName key)
		{
			if (d == null)
				return;
			if (d.ContainsKey(key))
			{
				PdfObject removed = d.Remove(key);
				string msg;
				if (removed == null)
					msg = "(" + dictionaryLabel + ") " + key.ToString() + ": null";
				else
					msg = "(" + dictionaryLabel + ") " + key.ToString() + ": " + removed.ToString();
				cleanedItemCallback(msg);
				removedItems.Add(msg);
			}
		}
	}
}
