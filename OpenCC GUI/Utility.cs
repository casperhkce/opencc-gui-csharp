namespace OpenCC_GUI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    internal static class FileListUtility
    {
        private static readonly int MaxTask;
        private static bool isRunning = false;
        private static IProgress<Reportinfo> progress;

        static FileListUtility()
        {
            progress = new Progress<Reportinfo>(info =>
            {
                if (info.Finishied)
                {
                    info.FileListItems.Remove(info.Item);
                }
                else
                {
                    info.Item.ErrorMessage = info.Message;
                }
            });

            MaxTask = Environment.ProcessorCount - 2;
            if (MaxTask < 1)
            {
                MaxTask = 1;
            }
        }

        public static void AppendFileList(BindingList<FileListItem> list, string[] fileNames)
        {
            list.RaiseListChangedEvents = false;
            foreach (var fileName in fileNames)
            {
                list.Add(new FileListItem() { FileName = fileName });
            }
            list.RaiseListChangedEvents = true;
            list.ResetBindings();
        }

        public static async void ConvertAndStoreFilesInList(BindingList<FileListItem> fileListItems, string configFileName, string outputFolder = null)
        {
            if (isRunning)
            {
                return;
            }
            isRunning = true;

            while (fileListItems.Count != 0)
            {
                SemaphoreSlim semaphore = new SemaphoreSlim(MaxTask, MaxTask);
                var fileListItemsClone = new FileListItem[fileListItems.Count];
                fileListItems.CopyTo(fileListItemsClone, 0);
                var tasks = new List<Task>(fileListItemsClone.Length);

                foreach (var item in fileListItemsClone)
                {
                    tasks.Add(Task.Run(() => ConvertTask(fileListItems, item, configFileName, outputFolder, semaphore)));
                }
                await Task.WhenAll(tasks);
            }

            isRunning = false;
        }

        private static void ConvertTask(BindingList<FileListItem> fileListItems, FileListItem item, string configFileName, string outputFolder, SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            if (!fileListItems.Contains(item))
            {
                semaphore.Release();
                return;
            }

            try
            {
                string content = TextUtility.readToUTF(item.FileName);
                string result = Converter.Convert(content, configFileName);
                string outputPath;
                if (outputFolder == null)
                {
                    outputPath = item.FileName;
                }
                else
                {
                    outputPath = outputFolder + "\\" + System.IO.Path.GetFileName(item.FileName);
                }

                System.IO.File.WriteAllText(outputPath, result, Encoding.UTF8);

                progress.Report(new Reportinfo { FileListItems = fileListItems, Finishied = true, Item = item });
            }
            catch (Exception exception)
            {
                progress.Report(new Reportinfo { FileListItems = fileListItems, Finishied = false, Item = item, Message = exception.Message });
            }
            finally
            {
                semaphore.Release();
            }
        }

        private class Reportinfo
        {
            public BindingList<FileListItem> FileListItems;
            public bool Finishied;
            public FileListItem Item;
            public string Message;
        }
    }

    internal static class TextUtility
    {
        public static void LoadTextToTextBox(TextBox textBox, string fileName)
        {
            try
            {
                textBox.Text = readToUTF(fileName);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        public static string readToUTF(string fileName)
        {
            string charset = "";
            string result = "";

            using (FileStream fs = File.OpenRead(fileName))
            {
                Ude.CharsetDetector cdet = new Ude.CharsetDetector();
                cdet.Feed(fs);
                cdet.DataEnd();
                if (cdet.Charset != null)
                {
                    Console.WriteLine("Charset: {0}, confidence: {1}",
                         cdet.Charset, cdet.Confidence);

                    charset = cdet.Charset;
                }
                else
                {
                    Console.WriteLine("Detection failed.");
                }
            }

            if (charset == "Big5")
            {
                result = readBig5(fileName);
            }
            if (charset == "gb18030")
            {
                result = readGB(fileName);
            }
            return result;
        }

        public static string readBig5(string filePath)
        {
            byte[] asciiBytes = null;

            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                //讀BIG5編碼bytes
                asciiBytes = new byte[fs.Length];
                fs.Read(asciiBytes, 0, (int)fs.Length);
            }

            //將BIG5轉成utf8編碼的bytes
            byte[] utf8Bytes = Encoding.Convert(Encoding.GetEncoding("BIG5"), Encoding.UTF8, asciiBytes);

            //將utf8 bytes轉成utf8字串
            UTF8Encoding encUtf8 = new UTF8Encoding();

            string utf8Str = encUtf8.GetString(utf8Bytes);

            return utf8Str;
        }

        public static string readGB(string filePath)
        {
            byte[] asciiBytes = null;

            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                //讀GB2312編碼bytes
                asciiBytes = new byte[fs.Length];
                fs.Read(asciiBytes, 0, (int)fs.Length);
            }

            //將GB2312轉成utf8編碼的bytes
            byte[] utf8Bytes = Encoding.Convert(Encoding.GetEncoding("GB2312"), Encoding.UTF8, asciiBytes);

            //將utf8 bytes轉成utf8字串
            UTF8Encoding encUtf8 = new UTF8Encoding();

            string utf8Str = encUtf8.GetString(utf8Bytes);

            return utf8Str;
        }
    }
}
