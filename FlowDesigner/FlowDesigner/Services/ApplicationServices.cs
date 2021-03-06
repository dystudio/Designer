﻿using System;
using FlowControl;

namespace FlowDesigner
{
    /// <summary>
    /// Simple service locator
    /// </summary>
    public class ServiceProvider : FlowControl.IServiceProvider
    {

        private IEditorService editorService = new WPFEditorService();
        private IMessageBoxService messageBoxService = new WPFMessageBoxService();
        private IStorageService storageService = new WPFStorageService();

        public IEditorService EditorService
        {
            get { return editorService; }
        }

        public IMessageBoxService MessageBoxService
        {
            get { return messageBoxService; }
        }

        public IStorageService StorageService
        {
            get { return storageService; }
        }

    }
}
