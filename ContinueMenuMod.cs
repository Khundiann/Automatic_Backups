using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

#if MONO
using ScheduleOne.Persistence;
using ScheduleOne.State;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.DevUtilities;
using System.Reflection;
using TMPro;
#else
using Il2CppTMPro;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.State;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.DevUtilities;
#endif

// Adds a "Restore" button to each game slot in the Continue menu
// Also creates a sub-menu to allow for selecting which backup to restore
namespace AutomaticBackups
{
    // Allows us to modify the Continue menu
    // We need a MonoBehavior so we can clone existing objects in the scene hierarchy (I don't know how to do this outside of a MonoBehavior)
    [RegisterTypeInIl2Cpp]
    public class ContinueMenuMod : MonoBehaviour
    {
        protected Transform continueMenu;               // Menu with save slots (Schedule I's built-in Menu)
        protected Transform loadMenu = null;            // Menu for our list of backups
        protected MenuScreen restoreBackupScreen = null;
        static public ContinueScreen continueScreen = null;
        public ImportScreen importScreen;

        void Awake()
        {
            Init(); // Calling the Awake code from another function will keep VS from making all the code gray
        }

        void Init()
        {
            // We should be attached to the MainMenu GameObject
            GameObject mainMenu = gameObject;

            // Continue object will be a child of MainMenu
            continueMenu = mainMenu.transform.Find("Continue");
            continueScreen = continueMenu.GetComponent<ContinueScreen>();
            Transform continueContainer = continueMenu.Find("Container");
            //DestroyImmediate(continueContainer.GetComponent<VerticalLayoutGroup>());

            // Add our "Restore" button to each slot            
            for (int i = 0; i < continueContainer.childCount; i++) // Enumerate children in an Il2Cpp-friendly manner
                AddButtonToSlot(continueContainer.GetChild(i), i);

            // Find the Import Screen
            importScreen = mainMenu.transform.Find("ImportScreen").GetComponent<ImportScreen>();

            // Instantiate scroll view and move all children to it
            /*GameObject scrollView = Instantiate(Core.scrollViewPrefab);
            Transform scrollViewContainer = scrollView.transform.Find("Viewport").Find("Content");
            Core.Log($"scrollViewContainer: {scrollViewContainer.ToString()}");

            // Iterate backwards over children so we can move them without messing up the ordering
            for (int i = continueContainer.childCount - 1; i >= 0; i--)
                continueContainer.GetChild(i).SetParent(scrollViewContainer, false);

            scrollView.transform.SetParent(continue_, false);*/
        }

        void AddButtonToSlot(Transform slot, int slotNumber)
        {
            Transform container = slot.Find("Container");
            Transform info = container.Find("Info");
            Transform exportButton = info.Find("Export");

            // Clone export button and modify it to become our Load button
            Transform loadButton = Instantiate(exportButton, info);
            loadButton.name = "Restore";
            Button button = loadButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
#if MONO
            button.onClick.AddListener(new UnityAction(() =>
            {
                LoadClicked(slotNumber);
            }));
#else
            button.onClick.AddListener(new Action(() =>
            {
                LoadClicked(slotNumber);
            }));
#endif

            // Update position
            loadButton.localPosition = new Vector3(-215.0f, 20.0f, 0.0f);

            // Update button text
            Transform text = loadButton.GetChild(0);
            TextMeshProUGUI ugui = text.GetComponent<TextMeshProUGUI>();
            ugui.text = "Restore";

            // Remove Schedule I's Export button component
            DestroyImmediate(loadButton.GetComponent<SaveExportButton>());
        }

        public void LoadClicked(int slot)
        {
            if (!loadMenu && !CreateLoadMenu())
                return;
            
            PopulateLoadMenu(slot);
            restoreBackupScreen.Open(true);
        }

        // Clone the "Continue" menu for our "Load/Restore" menu
        bool CreateLoadMenu()
        {
            if (Core.backupRestorePanelPrefab == null)
            {
                Core.Log("Failed to create the Restore Backup screen: the panel prefab was not loaded.");
                return false;
            }

            // Create the Restore menu based on our panel prefab
            loadMenu = Instantiate(Core.backupRestorePanelPrefab).transform;

            // Clone the background from the Continue menu
            Transform background = continueMenu.Find("Background");
            Transform title = continueMenu.Find("Title");
            CanvasGroup canvasGroup = loadMenu.GetComponent<CanvasGroup>();
            Transform scrollView = loadMenu.Find("Scroll View");
            MonoStateMachine stateParent = GetContinueScreenStateParent();
            if (background == null || title == null || canvasGroup == null ||
                scrollView == null || stateParent == null)
            {
                Core.Log("Failed to create the Restore Backup screen: a required menu component was not found.");
                Destroy(loadMenu.gameObject);
                loadMenu = null;
                return false;
            }

            Transform ourBG = Instantiate(background, loadMenu);
            ourBG.name = "Background";

            // Clone the title from the Continue menu
            Transform ourTitle = Instantiate(title, loadMenu);
            ourTitle.name = "Title";
            TextMeshProUGUI tmpUGUI = ourTitle.GetComponent<TextMeshProUGUI>();
            if (tmpUGUI == null)
            {
                Core.Log("Failed to create the Restore Backup screen: the title has no text component.");
                Destroy(loadMenu.gameObject);
                loadMenu = null;
                return false;
            }
            tmpUGUI.text = "Restore Backup";

            // Set the MainMenu as the parent and make sure the panel is hidden
            loadMenu.SetParent(transform, false);
            loadMenu.name = "RestoreBackup";
            loadMenu.gameObject.SetActive(false);

            // MenuScreen now opens through a MonoState. Configure both components
            // while the object is inactive so Awake sees the references that would
            // normally be assigned in the Unity editor.
            MonoState restoreState = loadMenu.gameObject.AddComponent<MonoState>();
            restoreState.InitializeDefaultParent(stateParent);

            restoreBackupScreen = loadMenu.gameObject.AddComponent<MenuScreen>();
            restoreBackupScreen.OpenOnStart = false;
            restoreBackupScreen.AttachLobbyToScreen = false;
            restoreBackupScreen.PreviousScreen = continueScreen;
            restoreBackupScreen.Group = canvasGroup;
            restoreBackupScreen.State = restoreState;

            // Make sure the ScrollView is drawn last
            scrollView.SetAsLastSibling();

            // Run Awake once. MenuScreen returns the object to its closed, inactive
            // state, after which Open() can safely push its state onto the stack.
            loadMenu.gameObject.SetActive(true);
            return true;
        }

        MonoStateMachine GetContinueScreenStateParent()
        {
            if (continueScreen == null || continueScreen.State == null)
                return null;

#if MONO
            FieldInfo defaultParentField = typeof(MonoState).GetField("_defaultParent", BindingFlags.Instance | BindingFlags.NonPublic);
            return defaultParentField?.GetValue(continueScreen.State) as MonoStateMachine;
#else
            return continueScreen.State._defaultParent;
#endif
        }

        // Add buttons to the load menu
        void PopulateLoadMenu(int slot)
        {
            // Get the scroll container
            Transform container = loadMenu.Find("Scroll View").Find("Viewport").Find("Content");

            // Remove any buttons that might have been added
            SettingsMenuMod.DestroyAllChildren(container);

            // Get the backup path for the given slot and ensure it exists
            string backupsPath = GetBackupsPath(slot);
            if (!Directory.Exists(backupsPath))
            {
                Core.Log($"No backups folder found at {backupsPath}");
                return;
            }

            // Add a button for each zip file in the backups folder
            DirectoryInfo dirInfo = new DirectoryInfo(backupsPath);
            var files = dirInfo.GetFiles("*.zip").OrderBy(f => Math.Min(f.CreationTimeUtc.Ticks, f.LastWriteTimeUtc.Ticks));
            foreach (FileInfo file in files)
            {
                Transform buttonParent = Instantiate(Core.backupRestoreButtonPrefab.transform, container);

                // Get the age of the file.
                // Use the earliest of the creation time or last modification time, for timestamps of copied-and-pasting backups to be identified correctly
                TimeSpan fileAge = DateTime.Now - file.CreationTime;
                if (file.LastWriteTimeUtc.Ticks < file.CreationTimeUtc.Ticks)
                    fileAge = DateTime.Now - file.LastWriteTime;

                // Update the button with descriptions of the file
                UpdateButtonText(buttonParent, file.Name, fileAge);

                // Add click action
                Button button = buttonParent.GetChild(0).GetComponent<Button>();
#if MONO
                button.onClick.AddListener(new UnityAction(() =>
                {
                    BackupSelected(file.FullName, slot);
                }));
#else
                button.onClick.AddListener(new Action(() =>
                {
                    BackupSelected(file.FullName, slot);
                }));
#endif
            }
        }

        string GetBackupsPath(int slot)
        {
            string backupsPath = Singleton<SaveManager>.Instance.BackupFolderPath;

            return Path.Combine(backupsPath, $"SaveGame_{slot + 1}");
        }

        void UpdateButtonText(Transform buttonParent, string fileName, TimeSpan fileAge)
        {
            // Add text for the filename
            Transform textGO = buttonParent.GetChild(0).GetChild(0);
            TextMeshProUGUI filenameText = textGO.gameObject.AddComponent<TextMeshProUGUI>();
            filenameText.fontSize = 16;
            filenameText.color = Color.white;
            filenameText.alignment = TextAlignmentOptions.Left;
            filenameText.text = "   " + fileName;

            // Add "Created" text
            Transform rightSide = buttonParent.GetChild(0).GetChild(1);
            Transform child1 = rightSide.GetChild(0);
            TextMeshProUGUI createdText = child1.gameObject.AddComponent<TextMeshProUGUI>();
            createdText.fontSize = 14;
            createdText.color = Color.white;
            createdText.alignment = TextAlignmentOptions.Left;
            createdText.text = "Created";

            // Add text for creation time
            Transform child2 = rightSide.GetChild(1);
            TextMeshProUGUI creationTimeText = child2.gameObject.AddComponent<TextMeshProUGUI>();
            creationTimeText.fontSize = 14;
            creationTimeText.color = Color.gray;
            creationTimeText.alignment = TextAlignmentOptions.Left;
            creationTimeText.text = " " + TimeLabel(fileAge);
        }

        // Adapted from SaveDisplay
        private string TimeLabel(TimeSpan fileAge)
        {
            int hours = Mathf.RoundToInt((float)fileAge.TotalHours);
            int num = hours / 24;
            if (num == 0)
            {
                if (hours == 0)
                    return "In the last hour";
                else if (hours == 1)
                    return "One hour ago";
                else
                    return $"{hours} hours ago";
            }
            if (num == 1)
            {
                return "Yesterday";
            }
            if (num > 365)
            {
                return "More than a year ago";
            }
            return num.ToString() + " days ago";
        }

        void BackupSelected(string fileName, int slot)
        {
            // Initialize the Import Screen confirmation (code adapted from SaveImportButton.Clicked)
            string text = fileName;
            if (!string.IsNullOrEmpty(text))
            {
                string tempImportPath = SaveImportButton.TempImportPath;
                if (Directory.Exists(tempImportPath))
                {
                    Directory.Delete(tempImportPath, true);
                }
                SaveImportButton.UnzipSaveFile(text, tempImportPath);
                importScreen.Initialize(slot, continueMenu.gameObject.GetComponent<ContinueScreen>());
                restoreBackupScreen.Close();
                importScreen.Open(true);
            }
        }
    }
}
