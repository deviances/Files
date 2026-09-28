// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Text.Json;
using Windows.Storage;

namespace Files.App.Utils
{
	internal sealed class WorkspacePilotWorkspace
	{
		public string Id { get; set; } = Guid.NewGuid().ToString("N");
		public string Name { get; set; } = "Workspace";
		public List<string> Pins { get; set; } = [];
		public List<string> SessionTabs { get; set; } = [];
		public int SelectedTabIndex { get; set; }
	}

	internal sealed class WorkspacePilotStore
	{
		public string? ActiveWorkspaceId { get; set; }
		public List<WorkspacePilotWorkspace> Workspaces { get; set; } = [];
	}

	internal static class WorkspacePilotManager
	{
		private static readonly object SyncRoot = new();
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true
		};

		private static WorkspacePilotStore store = LoadStore();

		public static event EventHandler? Changed;

		public static IReadOnlyList<WorkspacePilotWorkspace> Workspaces
		{
			get
			{
				lock (SyncRoot)
					return store.Workspaces.ToList().AsReadOnly();
			}
		}

		public static WorkspacePilotWorkspace ActiveWorkspace
		{
			get
			{
				lock (SyncRoot)
					return EnsureActiveWorkspaceUnsafe();
			}
		}

		public static bool IsPinned(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				return workspace.Pins.Contains(path, StringComparer.OrdinalIgnoreCase);
			}
		}

		public static void Pin(IEnumerable<string> paths)
		{
			var changed = false;

			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();

				foreach (var path in paths.Where(x => !string.IsNullOrWhiteSpace(x)))
				{
					if (workspace.Pins.Contains(path, StringComparer.OrdinalIgnoreCase))
						continue;

					workspace.Pins.Add(path);
					changed = true;
				}

				if (changed)
					SaveStoreUnsafe();
			}

			if (changed)
				Changed?.Invoke(null, EventArgs.Empty);
		}

		public static void Unpin(IEnumerable<string> paths)
		{
			var changed = false;
			var pathSet = paths
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (pathSet.Count == 0)
				return;

			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				changed = workspace.Pins.RemoveAll(pathSet.Contains) > 0;

				if (changed)
					SaveStoreUnsafe();
			}

			if (changed)
				Changed?.Invoke(null, EventArgs.Empty);
		}

		public static WorkspacePilotWorkspace CreateWorkspace(string? name = null)
		{
			WorkspacePilotWorkspace workspace;

			lock (SyncRoot)
			{
				workspace = new()
				{
					Name = string.IsNullOrWhiteSpace(name) ? $"Workspace {store.Workspaces.Count + 1}" : name.Trim()
				};

				store.Workspaces.Add(workspace);
				store.ActiveWorkspaceId = workspace.Id;
				SaveStoreUnsafe();
			}

			Changed?.Invoke(null, EventArgs.Empty);
			return workspace;
		}

		public static bool SetActiveWorkspace(string workspaceId)
		{
			var changed = false;

			lock (SyncRoot)
			{
				if (!store.Workspaces.Any(x => x.Id == workspaceId) || store.ActiveWorkspaceId == workspaceId)
					return false;

				store.ActiveWorkspaceId = workspaceId;
				SaveStoreUnsafe();
				changed = true;
			}

			if (changed)
				Changed?.Invoke(null, EventArgs.Empty);

			return changed;
		}

		public static bool RenameActiveWorkspace(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return false;

			var changed = false;

			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				var trimmed = name.Trim();

				if (workspace.Name == trimmed)
					return false;

				workspace.Name = trimmed;
				SaveStoreUnsafe();
				changed = true;
			}

			if (changed)
				Changed?.Invoke(null, EventArgs.Empty);

			return changed;
		}

		public static void ReplacePins(IEnumerable<string> paths)
		{
			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				workspace.Pins = paths
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				SaveStoreUnsafe();
			}

			Changed?.Invoke(null, EventArgs.Empty);
		}

		public static void SaveActiveSession(IEnumerable<string> serializedTabs, int selectedTabIndex)
		{
			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				workspace.SessionTabs = serializedTabs.ToList();
				workspace.SelectedTabIndex = selectedTabIndex;
				SaveStoreUnsafe();
			}
		}

		public static (List<string> Tabs, int SelectedIndex) GetActiveSession()
		{
			lock (SyncRoot)
			{
				var workspace = EnsureActiveWorkspaceUnsafe();
				return (workspace.SessionTabs.ToList(), workspace.SelectedTabIndex);
			}
		}

		private static WorkspacePilotStore LoadStore()
		{
			try
			{
				var path = GetStorePath();
				if (File.Exists(path))
				{
					var json = File.ReadAllText(path);
					var loaded = JsonSerializer.Deserialize<WorkspacePilotStore>(json, JsonOptions);
					if (loaded is not null)
					{
						NormalizeStore(loaded);
						return loaded;
					}
				}
			}
			catch
			{
				// A broken workspace file must never prevent Files from starting.
			}

			var initial = new WorkspacePilotStore();
			NormalizeStore(initial);
			return initial;
		}

		private static void NormalizeStore(WorkspacePilotStore value)
		{
			value.Workspaces ??= [];

			if (value.Workspaces.Count == 0)
				value.Workspaces.Add(new WorkspacePilotWorkspace { Name = "Default" });

			if (string.IsNullOrWhiteSpace(value.ActiveWorkspaceId) ||
				!value.Workspaces.Any(x => x.Id == value.ActiveWorkspaceId))
			{
				value.ActiveWorkspaceId = value.Workspaces[0].Id;
			}

			foreach (var workspace in value.Workspaces)
			{
				workspace.Pins ??= [];
				workspace.SessionTabs ??= [];
				if (string.IsNullOrWhiteSpace(workspace.Name))
					workspace.Name = "Workspace";
			}
		}

		private static WorkspacePilotWorkspace EnsureActiveWorkspaceUnsafe()
		{
			NormalizeStore(store);
			return store.Workspaces.First(x => x.Id == store.ActiveWorkspaceId);
		}

		private static string GetStorePath()
		{
			var root = Path.Combine(ApplicationData.Current.LocalFolder.Path, "WorkspacePilot");
			Directory.CreateDirectory(root);
			return Path.Combine(root, "workspaces.json");
		}

		private static void SaveStoreUnsafe()
		{
			try
			{
				var path = GetStorePath();
				var tempPath = path + ".tmp";
				File.WriteAllText(tempPath, JsonSerializer.Serialize(store, JsonOptions));
				File.Move(tempPath, path, true);
			}
			catch
			{
				// Workspace persistence is optional and must not crash Files.
			}
		}
	}
}
