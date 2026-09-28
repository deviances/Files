// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Storage;

namespace Files.App.Actions
{
	[GeneratedRichCommand]
	internal sealed partial class PinToWorkspaceAction : ObservableObject, IAction
	{
		private readonly IContentPageContext context;

		public string Label => "Pin to workspace";

		public string Description => "Add the selected folder to the active workspace.";

		public ActionCategory Category => ActionCategory.FileSystem;

		public RichGlyph Glyph => new(themedIconStyle: "App.ThemedIcons.FavoritePin");

		public bool IsExecutable => GetIsExecutable();

		public PinToWorkspaceAction()
		{
			context = Ioc.Default.GetRequiredService<IContentPageContext>();
			context.PropertyChanged += Context_PropertyChanged;
			WorkspacePilotManager.Changed += WorkspacePilotManager_Changed;
		}

		public Task ExecuteAsync(object? parameter = null)
		{
			if (context.HasSelection)
			{
				WorkspacePilotManager.Pin(context.SelectedItems.Select(x => x.ItemPath!));
			}
			else if (context.Folder is not null)
			{
				WorkspacePilotManager.Pin([context.Folder.ItemPath!]);
			}

			return Task.CompletedTask;
		}

		private bool GetIsExecutable()
		{
			if (context.PageType == ContentPageTypes.RecycleBin)
				return false;

			return context.HasSelection
				? context.SelectedItems.Count > 0 && context.SelectedItems.All(IsPinnable)
				: context.Folder is not null && IsPinnable(context.Folder);

			static bool IsFolder(ListedItem item)
				=> item.PrimaryItemAttribute is StorageItemTypes.Folder && !item.IsArchive;

			bool IsPinnable(ListedItem item)
				=> IsFolder(item) &&
					!string.IsNullOrWhiteSpace(item.ItemPath) &&
					!WorkspacePilotManager.IsPinned(item.ItemPath!);
		}

		private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(IContentPageContext.Folder) or nameof(IContentPageContext.SelectedItems))
				OnPropertyChanged(nameof(IsExecutable));
		}

		private void WorkspacePilotManager_Changed(object? sender, EventArgs e)
			=> OnPropertyChanged(nameof(IsExecutable));
	}
}
