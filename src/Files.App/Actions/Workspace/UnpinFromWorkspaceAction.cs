// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Storage;

namespace Files.App.Actions
{
	[GeneratedRichCommand]
	internal sealed partial class UnpinFromWorkspaceAction : ObservableObject, IAction
	{
		private readonly IContentPageContext context;

		public string Label => "Remove from workspace";

		public string Description => "Remove the selected folder from the active workspace.";

		public ActionCategory Category => ActionCategory.FileSystem;

		public RichGlyph Glyph => new(themedIconStyle: "App.ThemedIcons.FavoritePin");

		public bool IsExecutable => GetIsExecutable();

		public UnpinFromWorkspaceAction()
		{
			context = Ioc.Default.GetRequiredService<IContentPageContext>();
			context.PropertyChanged += Context_PropertyChanged;
			WorkspacePilotManager.Changed += WorkspacePilotManager_Changed;
		}

		public Task ExecuteAsync(object? parameter = null)
		{
			if (context.HasSelection)
			{
				WorkspacePilotManager.Unpin(context.SelectedItems.Select(x => x.ItemPath!));
			}
			else if (context.Folder is not null)
			{
				WorkspacePilotManager.Unpin([context.Folder.ItemPath!]);
			}

			return Task.CompletedTask;
		}

		private bool GetIsExecutable()
		{
			if (context.PageType == ContentPageTypes.RecycleBin)
				return false;

			return context.HasSelection
				? context.SelectedItems.Count > 0 && context.SelectedItems.All(IsPinnedFolder)
				: context.Folder is not null && IsPinnedFolder(context.Folder);

			bool IsPinnedFolder(ListedItem item)
				=> item.PrimaryItemAttribute is StorageItemTypes.Folder &&
					!item.IsArchive &&
					!string.IsNullOrWhiteSpace(item.ItemPath) &&
					WorkspacePilotManager.IsPinned(item.ItemPath!);
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
