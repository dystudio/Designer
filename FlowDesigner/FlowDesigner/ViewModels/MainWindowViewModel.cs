﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowControl;
using System;

namespace FlowDesigner
{
    public class MainWindowViewModel : INPCBase
    {

        private List<int> savedDiagrams = new List<int>();
        private int? savedDiagramId;
        private List<SelectableDesignerItemViewModelBase> itemsToRemove;
        private IMessageBoxService messageBoxService;
        private IStorageService storageService;
        private DiagramViewModel diagramViewModel = new DiagramViewModel();
        private bool isBusy = false;

        public MainWindowViewModel()
        {
            ApplicationServicesProvider.Instance.SetNewServiceProvider(new ServiceProvider());
            messageBoxService = ApplicationServicesProvider.Instance.Provider.MessageBoxService;
            storageService = ApplicationServicesProvider.Instance.Provider.StorageService;

            foreach (var savedDiagram in storageService.FetchAllDiagram())
            {
                savedDiagrams.Add(savedDiagram.Id);
            }

            ToolBoxViewModel = new ToolBoxViewModel();
            DiagramViewModel = new DiagramViewModel();

            DeleteSelectedItemsCommand = new SimpleCommand(ExecuteDeleteSelectedItemsCommand);
            CreateNewDiagramCommand = new SimpleCommand(ExecuteCreateNewDiagramCommand);
            SaveDiagramCommand = new SimpleCommand(ExecuteSaveDiagramCommand);
            LoadDiagramCommand = new SimpleCommand(ExecuteLoadDiagramCommand);
        }

        public SimpleCommand DeleteSelectedItemsCommand { get; private set; }
        public SimpleCommand CreateNewDiagramCommand { get; private set; }
        public SimpleCommand SaveDiagramCommand { get; private set; }
        public SimpleCommand LoadDiagramCommand { get; private set; }
        public ToolBoxViewModel ToolBoxViewModel { get; private set; }


        public DiagramViewModel DiagramViewModel
        {
            get
            {
                return diagramViewModel;
            }
            set
            {
                if (diagramViewModel != value)
                {
                    diagramViewModel = value;
                    NotifyChanged("DiagramViewModel");
                }
            }
        }

        public bool IsBusy
        {
            get
            {
                return isBusy;
            }
            set
            {
                if (isBusy != value)
                {
                    isBusy = value;
                    NotifyChanged("IsBusy");
                }
            }
        }


        public List<int> SavedDiagrams
        {
            get
            {
                return savedDiagrams;
            }
            set
            {
                if (savedDiagrams != value)
                {
                    savedDiagrams = value;
                    NotifyChanged("SavedDiagrams");
                }
            }
        }

        public int? SavedDiagramId
        {
            get
            {
                return savedDiagramId;
            }
            set
            {
                if (savedDiagramId != value)
                {
                    savedDiagramId = value;
                    NotifyChanged("SavedDiagramId");
                }
            }
        }

        private void ExecuteDeleteSelectedItemsCommand(object parameter)
        {
            itemsToRemove = DiagramViewModel.SelectedItems;
            List<SelectableDesignerItemViewModelBase> connectionsToAlsoRemove = new List<SelectableDesignerItemViewModelBase>();

            foreach (var connector in DiagramViewModel.Items.OfType<ConnectorViewModel>())
            {
                if (ItemsToDeleteHasConnector(itemsToRemove, connector.SourceConnectorInfo))
                {
                    connectionsToAlsoRemove.Add(connector);
                }

                if (ItemsToDeleteHasConnector(itemsToRemove, (FullyCreatedConnectorInfo)connector.SinkConnectorInfo))
                {
                    connectionsToAlsoRemove.Add(connector);
                }
            }
            itemsToRemove.AddRange(connectionsToAlsoRemove);
            foreach (var selectedItem in itemsToRemove)
            {
                DiagramViewModel.RemoveItemCommand.Execute(selectedItem);
            }
        }

        private void ExecuteCreateNewDiagramCommand(object parameter)
        {
            //ensure that itemsToRemove is cleared ready for any new changes within a session
            itemsToRemove = new List<SelectableDesignerItemViewModelBase>();
            SavedDiagramId = null;
            DiagramViewModel.CreateNewDiagramCommand.Execute(null);
        }

        private void ExecuteSaveDiagramCommand(object parameter)
        {
            if (!DiagramViewModel.Items.Any())
            {
                messageBoxService.ShowError("There must be at least one item in order save a diagram");
                return;
            }

            IsBusy = true;
            DiagramItem wholeDiagramToSave = null;

            Task<int> task = Task.Factory.StartNew<int>(() =>
                {

                    if (SavedDiagramId != null)
                    {
                        int currentSavedDiagramId = (int)SavedDiagramId.Value;
                        wholeDiagramToSave = storageService.FetchDiagram(currentSavedDiagramId);

                        //If we have a saved diagram, we need to make sure we clear out all the removed items that
                        //the user deleted as part of this work sesssion
                        foreach (var itemToRemove in itemsToRemove)
                        {
                            DeleteFromDatabase(wholeDiagramToSave, itemToRemove);
                        }
                        //start with empty collections of connections and items, which will be populated based on current diagram
                        wholeDiagramToSave.ConnectionIds = new List<int>();
                        wholeDiagramToSave.DesignerItems = new List<DiagramItemData>();
                    }
                    else
                    {
                        wholeDiagramToSave = new DiagramItem();
                    }

                    //ensure that itemsToRemove is cleared ready for any new changes within a session
                    itemsToRemove = new List<SelectableDesignerItemViewModelBase>();

                    foreach (var model in DiagramViewModel.Items)
                    {
                        DiagramItemData item = new DiagramItemData(model.Id, model.GetType());
                        storageService.SaveDiagramItem(item);
                        wholeDiagramToSave.DesignerItems.Add(item);
                    }
                    //Save all connections which should now have their Connection.DataItems filled in with correct Ids
                    foreach (var connectionVM in DiagramViewModel.Items.OfType<ConnectorViewModel>())
                    {
                        FullyCreatedConnectorInfo sinkConnector = connectionVM.SinkConnectorInfo as FullyCreatedConnectorInfo;
                        Connection connection = new Connection(
                            connectionVM.Id,
                            connectionVM.SourceConnectorInfo.DataItem.Id,
                            GetOrientationFromConnector(connectionVM.SourceConnectorInfo.Orientation),
                            GetTypeOfDiagramItem(connectionVM.SourceConnectorInfo.DataItem),
                            sinkConnector.DataItem.Id,
                            GetOrientationFromConnector(sinkConnector.Orientation),
                            GetTypeOfDiagramItem(sinkConnector.DataItem));

                        connectionVM.Id = storageService.SaveConnection(connection);
                        wholeDiagramToSave.ConnectionIds.Add(connectionVM.Id);
                    }

                    wholeDiagramToSave.Id = storageService.SaveDiagram(wholeDiagramToSave);
                    return wholeDiagramToSave.Id;
                });
            task.ContinueWith((ant) =>
            {
                int wholeDiagramToSaveId = ant.Result;
                if (!savedDiagrams.Contains(wholeDiagramToSaveId))
                {
                    List<int> newDiagrams = new List<int>(savedDiagrams);
                    newDiagrams.Add(wholeDiagramToSaveId);
                    SavedDiagrams = newDiagrams;

                }
                IsBusy = false;
                messageBoxService.ShowInformation(string.Format("Finished saving Diagram Id : {0}", wholeDiagramToSaveId));

            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private void ExecuteLoadDiagramCommand(object parameter)
        {
            IsBusy = true;
            DiagramItem wholeDiagramToLoad = null;
            if (SavedDiagramId == null)
            {
                messageBoxService.ShowError("You need to select a diagram to load");
                return;
            }

            Task<DiagramViewModel> task = Task.Factory.StartNew<DiagramViewModel>(() =>
                {
                    //ensure that itemsToRemove is cleared ready for any new changes within a session
                    itemsToRemove = new List<SelectableDesignerItemViewModelBase>();
                    DiagramViewModel diagramViewModel = new DiagramViewModel();

                    wholeDiagramToLoad = storageService.FetchDiagram((int)SavedDiagramId.Value);

                    //load diagram items
                    foreach (DiagramItemData diagramItemData in wholeDiagramToLoad.DesignerItems)
                    {
                        var designerItem = storageService.FetchDiagram(diagramItemData.ItemId);
                        var viewModel = Activator.CreateInstance(diagramItemData.ItemType) as DesignerItemViewModelBase;
                        viewModel.Id = designerItem.Id;
                        viewModel.Parent = diagramViewModel;
                        //viewModel.Left = designerItem.l
                        //viewModel.Top = designerItem.t 
                        diagramViewModel.Items.Add(viewModel);
                    }
                    //load connection items
                    foreach (int connectionId in wholeDiagramToLoad.ConnectionIds)
                    {
                        Connection connection = storageService.FetchConnection(connectionId);
                        DesignerItemViewModelBase sourceItem = GetConnectorDataItem(diagramViewModel, connection.SourceId, connection.SourceType);
                        ConnectorOrientation sourceConnectorOrientation = GetOrientationForConnector(connection.SourceOrientation);
                        FullyCreatedConnectorInfo sourceConnectorInfo = GetFullConnectorInfo(connection.Id, sourceItem, sourceConnectorOrientation);

                        DesignerItemViewModelBase sinkItem = GetConnectorDataItem(diagramViewModel, connection.SinkId, connection.SinkType);
                        ConnectorOrientation sinkConnectorOrientation = GetOrientationForConnector(connection.SinkOrientation);
                        FullyCreatedConnectorInfo sinkConnectorInfo = GetFullConnectorInfo(connection.Id, sinkItem, sinkConnectorOrientation);

                        ConnectorViewModel connectionVM = new ConnectorViewModel(connection.Id, diagramViewModel, sourceConnectorInfo, sinkConnectorInfo);
                        diagramViewModel.Items.Add(connectionVM);
                    }

                    return diagramViewModel;
                });
            task.ContinueWith((ant) =>
                {
                    this.DiagramViewModel = ant.Result;
                    IsBusy = false;
                    messageBoxService.ShowInformation(string.Format("Finished loading Diagram Id : {0}", wholeDiagramToLoad.Id));

                }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }


        private FullyCreatedConnectorInfo GetFullConnectorInfo(int connectorId, DesignerItemViewModelBase dataItem, ConnectorOrientation connectorOrientation)
        {
            switch (connectorOrientation)
            {
                case ConnectorOrientation.Left:
                    return dataItem.LeftConnector;
                case ConnectorOrientation.Right:
                    return dataItem.RightConnector;
                default:
                    throw new InvalidOperationException(
                        string.Format("Found invalid persisted Connector Orientation for Connector Id: {0}", connectorId));
            }
        }

        private Type GetTypeOfDiagramItem(DesignerItemViewModelBase vmType)
        {
            return vmType.GetType();
        }

        private DesignerItemViewModelBase GetConnectorDataItem(DiagramViewModel diagramViewModel, int conectorDataItemId, Type connectorDataItemType)
        {
            DesignerItemViewModelBase dataItem = null;
            dataItem = diagramViewModel.Items.Single(x => x.Id == conectorDataItemId) as DesignerItemViewModelBase;
            return dataItem;
        }

        private Orientation GetOrientationFromConnector(ConnectorOrientation connectorOrientation)
        {
            Orientation result = Orientation.None;
            switch (connectorOrientation)
            {
                case ConnectorOrientation.Left:
                    result = Orientation.Left;
                    break;
                case ConnectorOrientation.Right:
                    result = Orientation.Right;
                    break;
            }
            return result;
        }

        private ConnectorOrientation GetOrientationForConnector(Orientation persistedOrientation)
        {
            ConnectorOrientation result = ConnectorOrientation.None;
            switch (persistedOrientation)
            {
                case Orientation.Left:
                    result = ConnectorOrientation.Left;
                    break;
                case Orientation.Right:
                    result = ConnectorOrientation.Right;
                    break;
            }
            return result;
        }

        private bool ItemsToDeleteHasConnector(List<SelectableDesignerItemViewModelBase> itemsToRemove, FullyCreatedConnectorInfo connector)
        {
            return itemsToRemove.Contains(connector.DataItem);
        }

        private void DeleteFromDatabase(DiagramItem wholeDiagramToAdjust, SelectableDesignerItemViewModelBase itemToDelete)
        {
            if (itemToDelete is ConnectorViewModel)
            {
                wholeDiagramToAdjust.ConnectionIds.Remove(itemToDelete.Id);
                storageService.DeleteConnection(itemToDelete.Id);
            }
            else
            {
                //DiagramItemData diagramItemToRemoveFromParent = wholeDiagramToAdjust.DesignerItems.Where(x => x.ItemId == itemToDelete.Id && x.ItemType == typeof(SettingsDesignerItem)).Single();
                var diagramItemToRemoveFromParent = wholeDiagramToAdjust.DesignerItems.Where(x => x.ItemId == itemToDelete.Id).Single();
                wholeDiagramToAdjust.DesignerItems.Remove(diagramItemToRemoveFromParent);
            }
            storageService.SaveDiagram(wholeDiagramToAdjust);
        }

    }
}
