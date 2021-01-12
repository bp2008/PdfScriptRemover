# PdfScriptRemover
Removes JavaScript and embedded files from PDFs, powered by iText 7 for .NET

### Usage

Download the latest release.  It is a command line app, so use it like one.

```
PdfScriptRemover 1.0.0.0
 - powered by iText7 for .NET -
 - licensed by GNU AFFERO GENERAL PUBLIC LICENSE -

This program removes embedded JavaScript from a PDF file.

Usage: PdfScriptRemover.exe inputFile outputFile
```

For convenience, you can provide a directory path as `outputFile` and the output file name will be copied from the input file.  You can also provide a directory path as `inputFile` and the program will recursively run itself on the PDF files contained within the input directory.  When using directory input, you must also use directory output.

A record of each object removed from the PDF is written to Standard Output.  If nothing is removed, nothing is written to standard output, and the output file is not written.
