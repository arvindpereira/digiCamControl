﻿#region Licence

// Distributed under MIT License
// ===========================================================
// 
// digiCamControl - DSLR camera remote control open source software
// Copyright (C) 2014 Duka Istvan
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY,FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY 
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
// THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using CameraControl.Core;
using CameraControl.Core.Classes;
using CameraControl.Devices;
using GalaSoft.MvvmLight.Command;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Controls;
using Clipboard = System.Windows.Clipboard;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

#endregion

namespace CameraControl.Layouts
{
    public class LayoutBase : UserControl
    {
        public ListBox ImageLIst { get; set; }
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private FileItem _selectedItem = null;

        public LayoutBase()
        {
            CopyNameClipboardCommand =
                new RelayCommand(
                    delegate { Clipboard.SetText(ServiceProvider.Settings.SelectedBitmap.FileItem.FileName); });
            OpenExplorerCommand = new RelayCommand(OpenInExplorer);
            OpenViewerCommand = new RelayCommand(OpenViewer);
            DeleteItemCommand = new RelayCommand(DeleteItem);
            RestoreCommand = new RelayCommand(Restore);
            ImageDoubleClickCommand =
                new RelayCommand(
                    () => ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.FullScreenWnd_Show));
            _worker.DoWork += worker_DoWork;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;
        }

        public void UnInit()
        {
            _worker.DoWork -= worker_DoWork;
            _worker.RunWorkerCompleted -= _worker_RunWorkerCompleted;
            ServiceProvider.Settings.PropertyChanged -= Settings_PropertyChanged;
            ServiceProvider.WindowsManager.Event -= Trigger_Event;
            ImageLIst.SelectionChanged -= ImageLIst_SelectionChanged;
        }

        private void Restore()
        {
            if (ServiceProvider.Settings.SelectedBitmap == null ||
                ServiceProvider.Settings.SelectedBitmap.FileItem == null)
                return;
            var item = ServiceProvider.Settings.SelectedBitmap.FileItem;
            if (File.Exists(item.BackupFileName))
            {
                try
                {
                    PhotoUtils.WaitForFile(item.FileName);
                    File.Copy(item.BackupFileName, item.FileName, true);
                    item.RemoveThumbs();
                    item.IsLoaded = false;
                    ServiceProvider.WindowsManager.ExecuteCommand(WindowsCmdConsts.Refresh_Image);
                }
                catch (Exception ex)
                {
                    Log.Error("Error restore", ex);
                }
              
            }
        }

        private void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_selectedItem != ServiceProvider.Settings.SelectedBitmap.FileItem)
            {
                ServiceProvider.Settings.SelectedBitmap.FileItem = _selectedItem;
                _worker.RunWorkerAsync(_selectedItem);
            }
        }


        public RelayCommand CopyNameClipboardCommand { get; private set; }

        public RelayCommand OpenExplorerCommand { get; private set; }

        public RelayCommand OpenViewerCommand { get; private set; }

        public RelayCommand DeleteItemCommand { get; private set; }
        
        public RelayCommand RestoreCommand { get; private set; }

        public RelayCommand ImageDoubleClickCommand { get; private set; }

        private void OpenInExplorer()
        {
            if (ServiceProvider.Settings.SelectedBitmap == null ||
                ServiceProvider.Settings.SelectedBitmap.FileItem == null)
                return;
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "explorer";
                processStartInfo.UseShellExecute = true;
                processStartInfo.WindowStyle = ProcessWindowStyle.Normal;
                processStartInfo.Arguments =
                    string.Format("/e,/select,\"{0}\"", ServiceProvider.Settings.SelectedBitmap.FileItem.FileName);
                Process.Start(processStartInfo);
            }
            catch (Exception exception)
            {
                Log.Error("Error to show file in explorer", exception);
            }
        }

        private void OpenViewer()
        {
            if (ServiceProvider.Settings.SelectedBitmap == null ||
                ServiceProvider.Settings.SelectedBitmap.FileItem == null)
                return;
            if (!string.IsNullOrWhiteSpace(ServiceProvider.Settings.ExternalViewer) &&
                File.Exists(ServiceProvider.Settings.ExternalViewer))
            {
                PhotoUtils.Run(ServiceProvider.Settings.ExternalViewer,
                    ServiceProvider.Settings.SelectedBitmap.FileItem.FileName, ProcessWindowStyle.Maximized);
            }
            else
            {
                PhotoUtils.Run(ServiceProvider.Settings.SelectedBitmap.FileItem.FileName, "",
                    ProcessWindowStyle.Maximized);
            }
        }

        private void DeleteItem()
        {
            List<FileItem> filestodelete = new List<FileItem>();
            try
            {
                filestodelete.AddRange(
                    ServiceProvider.Settings.DefaultSession.Files.Where(fileItem => fileItem.IsChecked));

                if (ServiceProvider.Settings.SelectedBitmap != null &&
                    ServiceProvider.Settings.SelectedBitmap.FileItem != null &&
                    filestodelete.Count == 0)
                    filestodelete.Add(ServiceProvider.Settings.SelectedBitmap.FileItem);

                if (filestodelete.Count == 0)
                    return;
                int selectedindex = ImageLIst.Items.IndexOf(filestodelete[0]);

                bool delete = false;
                if (filestodelete.Count > 1)
                {
                    delete = MessageBox.Show("Multile files are selected !! Do you really want to delete selected files ?", "Delete files",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                }
                else
                {
                    delete = MessageBox.Show("Do you really want to delete selected file ?", "Delete file",
                        MessageBoxButton.YesNo) == MessageBoxResult.Yes;

                }
                if (delete)
                                    {
                    foreach (FileItem fileItem in filestodelete)
                    {
                        if ((ServiceProvider.Settings.SelectedBitmap != null &&
                             ServiceProvider.Settings.SelectedBitmap.FileItem != null &&
                             fileItem.FileName == ServiceProvider.Settings.SelectedBitmap.FileItem.FileName))
                        {
                            ServiceProvider.Settings.SelectedBitmap.DisplayImage = null;
                        }
                        if (File.Exists(fileItem.FileName))
                            FileSystem.DeleteFile(fileItem.FileName, UIOption.OnlyErrorDialogs,
                                                  RecycleOption.SendToRecycleBin);
                        fileItem.RemoveThumbs();
                        ServiceProvider.Settings.DefaultSession.Files.Remove(fileItem);
                    }
                    if (selectedindex < ImageLIst.Items.Count)
                    {
                        ImageLIst.SelectedIndex = selectedindex + 1;
                        ImageLIst.SelectedIndex = selectedindex - 1;
                        FileItem item = ImageLIst.SelectedItem as FileItem;

                        if (item != null)
                            ImageLIst.ScrollIntoView(item);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Error("Error to delete file", exception);
            }
        }

        public void InitServices()
        {
            ServiceProvider.Settings.PropertyChanged += Settings_PropertyChanged;
            ServiceProvider.WindowsManager.Event += Trigger_Event;
            ImageLIst.SelectionChanged += ImageLIst_SelectionChanged;
            if (ServiceProvider.Settings.SelectedBitmap != null &&
                ServiceProvider.Settings.SelectedBitmap.FileItem != null)
            {
                ImageLIst.SelectedItem = ServiceProvider.Settings.SelectedBitmap.FileItem;
                ImageLIst.ScrollIntoView(ImageLIst.SelectedItem);
            }
            else
            {
                if (ServiceProvider.Settings.DefaultSession.Files.Count > 0)
                    ImageLIst.SelectedIndex = 0;
            }
        }

        private void ImageLIst_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                _selectedItem = e.AddedItems[0] as FileItem;
                if (_worker.IsBusy)
                    return;
                FileItem item = e.AddedItems[0] as FileItem;
                if (item != null)
                {
                    ServiceProvider.Settings.SelectedBitmap.SetFileItem(item);
                    _worker.RunWorkerAsync(false);
                }
            }
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (ServiceProvider.Settings.SelectedBitmap.FileItem == null)
                return;
            bool fullres = e.Argument is bool && (bool) e.Argument;
            ServiceProvider.Settings.ImageLoading = fullres ||
                                                    !ServiceProvider.Settings.SelectedBitmap.FileItem.IsLoaded;
            BitmapLoader.Instance.GenerateCache(ServiceProvider.Settings.SelectedBitmap.FileItem);
            ServiceProvider.Settings.SelectedBitmap.DisplayImage =
                BitmapLoader.Instance.LoadImage(ServiceProvider.Settings.SelectedBitmap.FileItem, fullres);
            BitmapLoader.Instance.SetData(ServiceProvider.Settings.SelectedBitmap,
                              ServiceProvider.Settings.SelectedBitmap.FileItem);
            BitmapLoader.Instance.Highlight(ServiceProvider.Settings.SelectedBitmap,
                                            ServiceProvider.Settings.HighlightUnderExp,
                                            ServiceProvider.Settings.HighlightOverExp);
            ServiceProvider.Settings.SelectedBitmap.FullResLoaded = fullres;
            ServiceProvider.Settings.ImageLoading = false;
            OnImageLoaded();
            GC.Collect();
        }

        public virtual void OnImageLoaded()
        {
        }

        public void LoadFullRes()
        {
            if (_worker.IsBusy)
                return;
            if (ServiceProvider.Settings.SelectedBitmap.FullResLoaded)
                return;
            _worker.RunWorkerAsync(true);
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "DefaultSession")
            {
                Thread.Sleep(1000);
                Dispatcher.Invoke(new Action(delegate
                                                 {
                                                     ImageLIst.SelectedIndex = 0;
                                                     if (ImageLIst.Items.Count == 0)
                                                         ServiceProvider.Settings.SelectedBitmap.DisplayImage = null;
                                                 }));
            }
            if (e.PropertyName == "HighlightOverExp")
            {
                if (!_worker.IsBusy)
                {
                    _worker.RunWorkerAsync(false);
                }
            }
            if (e.PropertyName == "HighlightUnderExp")
            {
                if (!_worker.IsBusy)
                {
                    _worker.RunWorkerAsync(false);
                }
            }
            if (e.PropertyName == "ShowFocusPoints")
            {
                if (!_worker.IsBusy)
                {
                    _worker.RunWorkerAsync(false);
                }
            }
            if (e.PropertyName == "FlipPreview")
            {
                if (!_worker.IsBusy)
                {
                    _worker.RunWorkerAsync(false);
                }
            }
            if (e.PropertyName == "LowMemoryUsage")
            {
                if (!_worker.IsBusy)
                {
                    _worker.RunWorkerAsync(false);
                }
            }
        }

        private void Trigger_Event(string cmd, object o)
        {
            ImageLIst.Dispatcher.Invoke(new Action(delegate
                                                       {
                                                           switch (cmd)
                                                           {
                                                               case WindowsCmdConsts.Next_Image:
                                                                   if (ImageLIst.SelectedIndex <
                                                                       ImageLIst.Items.Count - 1)
                                                                   {
                                                                       FileItem item =
                                                                           ImageLIst.SelectedItem as FileItem;
                                                                       if (item != null)
                                                                       {
                                                                           int ind = ImageLIst.Items.IndexOf(item);
                                                                           ImageLIst.SelectedIndex = ind + 1;
                                                                       }
                                                                       item = ImageLIst.SelectedItem as FileItem;
                                                                       if (item != null)
                                                                           ImageLIst.ScrollIntoView(item);
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Prev_Image:
                                                                   if (ImageLIst.SelectedIndex > 0)
                                                                   {
                                                                       FileItem item =
                                                                           ImageLIst.SelectedItem as FileItem;
                                                                       if (item != null)
                                                                       {
                                                                           int ind = ImageLIst.Items.IndexOf(item);
                                                                           ImageLIst.SelectedIndex = ind - 1;
                                                                       }
                                                                       item = ImageLIst.SelectedItem as FileItem;
                                                                       if (item != null)
                                                                           ImageLIst.ScrollIntoView(item);
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Like_Image:
                                                                   if (ImageLIst.SelectedItem != null)
                                                                   {
                                                                       FileItem item = null;
                                                                       if (o != null)
                                                                       {
                                                                           item = ServiceProvider.Settings.DefaultSession.GetByName(o as string);
                                                                       }
                                                                       else
                                                                       {
                                                                           item = ImageLIst.SelectedItem as FileItem;
                                                                       }
                                                                       if (item != null)
                                                                       {
                                                                           item.IsLiked = !item.IsLiked;
                                                                       }
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Unlike_Image:
                                                                   if (ImageLIst.SelectedItem != null)
                                                                   {
                                                                       FileItem item = null;
                                                                       if (o != null)
                                                                       {
                                                                           item =
                                                                               ServiceProvider.Settings.DefaultSession
                                                                                   .GetByName(o as string);
                                                                       }
                                                                       else
                                                                       {
                                                                           item = ImageLIst.SelectedItem as FileItem;
                                                                       }
                                                                       if (item != null)
                                                                       {
                                                                           item.IsUnLiked = !item.IsUnLiked;
                                                                       }
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Del_Image:
                                                                   {
                                                                       DeleteItem();
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Select_Image:
                                                                   FileItem fileItem = o as FileItem;
                                                                   if (fileItem != null)
                                                                   {
                                                                       ImageLIst.SelectedValue = fileItem;
                                                                       ImageLIst.ScrollIntoView(fileItem);
                                                                   }
                                                                   break;
                                                               case WindowsCmdConsts.Refresh_Image:
                                                                   if (!_worker.IsBusy)
                                                                   {
                                                                       _worker.RunWorkerAsync(false);
                                                                   }
                                                                   break;
                                                           }
                                                       }));
        }
    }
}