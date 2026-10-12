using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

#if MONO
using ScheduleOne;
using ScheduleOne.Persistence;
using ScheduleOne.State;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.DevUtilities;
using System.Reflection;
using TMPro;
#else
using Il2CppTMPro;
using Il2CppScheduleOne;
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
        protected UIContentPanel continuePanel = null;
        protected UIScreen restoreBackupUiScreen = null;
        protected UIContentPanel restoreBackupPanel = null;
        protected GameObject controllerSelectionFrameTemplate = null;
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
            continuePanel = continueMenu.GetComponent<UIContentPanel>();
            controllerSelectionFrameTemplate = FindSelectionFrame(continueMenu);
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
            if (controllerSelectionFrameTemplate == null)
                controllerSelectionFrameTemplate = FindSelectionFrame(slot);

            Transform container = slot.Find("Container");
            Transform info = container.Find("Info");
            Transform exportButton = info.Find("Export");

            // Clone export button and modify it to become our Load button
            Transform loadButton = Instantiate(exportButton, info);
            loadButton.name = "Restore";
            Button button = loadButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();

            UISelectable selectable = continuePanel == null ? null : AddControllerSelectable(loadButton.gameObject, button, continuePanel);
            if (selectable == null)
                Core.Log($"Failed to register controller navigation for the Restore button in save slot {slotNumber + 1}.");
            else
            {
#if MONO
                selectable.OnTrigger.AddListener(new UnityAction(() => LoadClicked(slotNumber)));
#else
                selectable.OnTrigger.AddListener(new Action(() => LoadClicked(slotNumber)));
#endif
            }

            // Update position
            loadButton.localPosition = new Vector3(-215.0f, 20.0f, 0.0f);

            // Update button text
            Transform text = loadButton.GetChild(0);
            TextMeshProUGUI ugui = text.GetComponent<TextMeshProUGUI>();
            ugui.text = "Restore";

            // Remove Schedule I's Export button component
            DestroyImmediate(loadButton.GetComponent<SaveExportButton>());
        }

        UISelectable AddControllerSelectable(GameObject target, Button button, UIPanel panel)
        {
            if (target == null || button == null || panel == null)
                return null;

            UISelectable selectable = target.GetComponent<UISelectable>();
            if (selectable == null)
                selectable = target.AddComponent<UISelectable>();

            if (selectable == null)
                return null;

            if (!InitializeNavigationOverride(selectable))
                return null;

            AddSelectionFrame(selectable, target.transform);

            // Route mouse and controller activation through UISelectable. Keeping a
            // Button.onClick callback as well can invoke the action twice on gamepad.
            button.onClick.RemoveAllListeners();
            // Replace the serialized events rather than calling RemoveAllListeners(),
            // which does not remove persistent prefab listeners.
            selectable.OnTrigger = new UnityEvent();
            selectable.OnSelected = new UnityEvent();
            selectable.OnDeselected = new UnityEvent();

            // The cloned buttons use solid white for their selected tint. Match the
            // normal highlighted tint so controller focus remains visible and legible.
            ColorBlock colors = button.colors;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

#if MONO
            selectable.OnSelected.AddListener(new UnityAction(() => button.OnSelect(null)));
            selectable.OnDeselected.AddListener(new UnityAction(() => button.OnDeselect(null)));
#else
            selectable.OnSelected.AddListener(new Action(() => button.OnSelect(null)));
            selectable.OnDeselected.AddListener(new Action(() => button.OnDeselect(null)));
#endif

            return panel.AddSelectable(selectable) ? selectable : null;
        }

        GameObject FindSelectionFrame(Transform root)
        {
            UISelectable[] selectables = root.GetComponentsInChildren<UISelectable>(true);
            GameObject activeFallback = null;
            for (int i = 0; i < selectables.Length; i++)
            {
                GameObject selectedImage = GetSelectedImage(selectables[i]);
                if (selectedImage == null)
                    continue;

                // Cloning an inactive frame lets us remove its non-visual scripts
                // before Unity invokes their Awake methods.
                if (!selectedImage.activeSelf)
                    return selectedImage;

                if (activeFallback == null)
                    activeFallback = selectedImage;
            }

            return activeFallback;
        }

        void AddSelectionFrame(UISelectable selectable, Transform parent)
        {
            if (controllerSelectionFrameTemplate == null || GetSelectedImage(selectable) != null)
                return;

            bool templateWasActive = controllerSelectionFrameTemplate.activeSelf;
            if (templateWasActive)
                controllerSelectionFrameTemplate.SetActive(false);

            GameObject selectedImage = Instantiate(controllerSelectionFrameTemplate, parent, false);

            if (templateWasActive)
                controllerSelectionFrameTemplate.SetActive(true);

            selectedImage.name = "Controller Selection";

            // Keep only Unity UI rendering components. The native frame can contain
            // behaviours whose serialized references are not valid on a runtime clone.
            Component[] components = selectedImage.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (IsSelectionFrameVisual(component))
                    continue;

                DestroyImmediate(component);
            }

            RectTransform selectedRect = selectedImage.GetComponent<RectTransform>();
            if (selectedRect == null)
            {
                Destroy(selectedImage);
                return;
            }

            selectedRect.anchorMin = Vector2.zero;
            selectedRect.anchorMax = Vector2.one;
            selectedRect.offsetMin = Vector2.zero;
            selectedRect.offsetMax = Vector2.zero;
            selectedRect.localScale = Vector3.one;
            selectedRect.SetAsLastSibling();

            Graphic[] graphics = selectedImage.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;

            selectedImage.SetActive(false);
            SetSelectedImage(selectable, selectedImage);
        }

        bool IsSelectionFrameVisual(Component component)
        {
#if MONO
            return component is Transform || component is CanvasRenderer || component is Graphic ||
                   component is BaseMeshEffect || component is CanvasGroup;
#else
            // Components returned through the IL2CPP base wrapper do not reliably
            // satisfy normal C# type tests. Native casts preserve the frame's
            // RectTransforms and graphics while still allowing its scripts to be removed.
            return component.TryCast<Transform>() != null ||
                   component.TryCast<CanvasRenderer>() != null ||
                   component.TryCast<Graphic>() != null ||
                   component.TryCast<BaseMeshEffect>() != null ||
                   component.TryCast<CanvasGroup>() != null;
#endif
        }

        GameObject GetSelectedImage(UISelectable selectable)
        {
#if MONO
            FieldInfo selectedImageField = typeof(UISelectable).GetField("selectedImage", BindingFlags.Instance | BindingFlags.NonPublic);
            return selectedImageField?.GetValue(selectable) as GameObject;
#else
            return selectable.selectedImage;
#endif
        }

        void SetSelectedImage(UISelectable selectable, GameObject selectedImage)
        {
#if MONO
            FieldInfo selectedImageField = typeof(UISelectable).GetField("selectedImage", BindingFlags.Instance | BindingFlags.NonPublic);
            selectedImageField?.SetValue(selectable, selectedImage);
#else
            selectable.selectedImage = selectedImage;
#endif
        }

        NavigationOverride<UISelectable> CreateSelectableNavigationOverride()
        {
            NavigationOverride<UISelectable> navigation = new NavigationOverride<UISelectable>();
            navigation.Up = new NavigationOverride<UISelectable>.OverrideElement<UISelectable>();
            navigation.Down = new NavigationOverride<UISelectable>.OverrideElement<UISelectable>();
            navigation.Left = new NavigationOverride<UISelectable>.OverrideElement<UISelectable>();
            navigation.Right = new NavigationOverride<UISelectable>.OverrideElement<UISelectable>();
            return navigation;
        }

        NavigationOverride<UIPanel> CreatePanelNavigationOverride()
        {
            NavigationOverride<UIPanel> navigation = new NavigationOverride<UIPanel>();
            navigation.Up = new NavigationOverride<UIPanel>.OverrideElement<UIPanel>();
            navigation.Down = new NavigationOverride<UIPanel>.OverrideElement<UIPanel>();
            navigation.Left = new NavigationOverride<UIPanel>.OverrideElement<UIPanel>();
            navigation.Right = new NavigationOverride<UIPanel>.OverrideElement<UIPanel>();
            return navigation;
        }

        bool InitializeNavigationOverride(UISelectable selectable)
        {
            NavigationOverride<UISelectable> navigation = selectable.NavigationOverride;
            if (navigation != null && navigation.Up != null && navigation.Down != null &&
                navigation.Left != null && navigation.Right != null)
                return true;

            navigation = CreateSelectableNavigationOverride();
#if MONO
            FieldInfo navigationField = typeof(UISelectable).GetField("navigationOverride", BindingFlags.Instance | BindingFlags.NonPublic);
            if (navigationField == null)
                return false;
            navigationField.SetValue(selectable, navigation);
#else
            selectable.navigationOverride = navigation;
#endif
            return true;
        }

        bool InitializeNavigationOverride(UIPanel panel)
        {
            NavigationOverride<UIPanel> navigation = panel.NavigationOverride;
            if (navigation != null && navigation.Up != null && navigation.Down != null &&
                navigation.Left != null && navigation.Right != null)
                return true;

            navigation = CreatePanelNavigationOverride();
#if MONO
            FieldInfo navigationField = typeof(UIPanel).GetField("navigationOverride", BindingFlags.Instance | BindingFlags.NonPublic);
            if (navigationField == null)
                return false;
            navigationField.SetValue(panel, navigation);
#else
            panel.navigationOverride = navigation;
#endif
            return true;
        }

        public void LoadClicked(int slot)
        {
            if (!loadMenu && !CreateLoadMenu())
                return;
            
            int backupCount = PopulateLoadMenu(slot);
            restoreBackupScreen.Open(true);

            if (GameInput.GetCurrentInputDeviceIsGamepad() && backupCount > 0)
                restoreBackupPanel.SelectSelectable(backupCount - 1, false);
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

            // The asset-bundle prefab predates the game's controller navigation
            // system, so give the restore menu a screen and a scrollable panel.
            restoreBackupUiScreen = loadMenu.gameObject.AddComponent<UIScreen>();
            restoreBackupPanel = scrollView.gameObject.AddComponent<UIContentPanel>();
            ScrollRect restoreScrollRect = scrollView.GetComponent<ScrollRect>();
            if (restoreScrollRect == null)
            {
                Core.Log("Failed to create the Restore Backup screen: the scroll view has no ScrollRect component.");
                Destroy(loadMenu.gameObject);
                loadMenu = null;
                restoreBackupUiScreen = null;
                restoreBackupPanel = null;
                return false;
            }
            if (!InitializeNavigationOverride(restoreBackupPanel))
            {
                Core.Log("Failed to create the Restore Backup screen: controller navigation could not be initialized.");
                Destroy(loadMenu.gameObject);
                loadMenu = null;
                restoreBackupUiScreen = null;
                restoreBackupPanel = null;
                return false;
            }
            if (!SetPanelScrollRect(restoreBackupPanel, restoreScrollRect))
            {
                Core.Log("Failed to create the Restore Backup screen: the panel's ScrollRect reference could not be assigned.");
                Destroy(loadMenu.gameObject);
                loadMenu = null;
                restoreBackupUiScreen = null;
                restoreBackupPanel = null;
                return false;
            }
            restoreBackupUiScreen.AddPanel(restoreBackupPanel);

            restoreBackupScreen = loadMenu.gameObject.AddComponent<MenuScreen>();
            restoreBackupScreen.OpenOnStart = false;
            restoreBackupScreen.AttachLobbyToScreen = false;
            restoreBackupScreen.PreviousScreen = continueScreen;
            restoreBackupScreen.Group = canvasGroup;
            restoreBackupScreen.State = restoreState;
            restoreBackupScreen.uiScreen = restoreBackupUiScreen;
            restoreBackupScreen.uiPanel = restoreBackupPanel;

            // Make sure the ScrollView is drawn last
            scrollView.SetAsLastSibling();

            // Run Awake once. MenuScreen returns the object to its closed, inactive
            // state, after which Open() can safely push its state onto the stack.
            loadMenu.gameObject.SetActive(true);
            return true;
        }

        bool SetPanelScrollRect(UIContentPanel panel, ScrollRect scrollRect)
        {
#if MONO
            FieldInfo scrollRectField = typeof(UIPanel).GetField("scrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
            if (scrollRectField == null)
                return false;

            scrollRectField.SetValue(panel, scrollRect);
#else
            panel.scrollRect = scrollRect;
#endif
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
        int PopulateLoadMenu(int slot)
        {
            // Get the scroll container
            Transform container = loadMenu.Find("Scroll View").Find("Viewport").Find("Content");

            // Remove any buttons that might have been added
            restoreBackupPanel.ClearAllSelectables();
            SettingsMenuMod.DestroyAllChildren(container);

            // Get the backup path for the given slot and ensure it exists
            string backupsPath = GetBackupsPath(slot);
            if (!Directory.Exists(backupsPath))
            {
                Core.Log($"No backups folder found at {backupsPath}");
                return 0;
            }

            // Add a button for each zip file in the backups folder
            DirectoryInfo dirInfo = new DirectoryInfo(backupsPath);
            var files = dirInfo.GetFiles("*.zip").OrderBy(f => Math.Min(f.CreationTimeUtc.Ticks, f.LastWriteTimeUtc.Ticks));
            int backupCount = 0;
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

                Button button = buttonParent.GetChild(0).GetComponent<Button>();
                UISelectable selectable = AddControllerSelectable(button.gameObject, button, restoreBackupPanel);
                if (selectable == null)
                    Core.Log($"Failed to register controller navigation for backup {file.Name}.");
                else
                {
#if MONO
                    selectable.OnTrigger.AddListener(new UnityAction(() => BackupSelected(file.FullName, slot)));
#else
                    selectable.OnTrigger.AddListener(new Action(() => BackupSelected(file.FullName, slot)));
#endif
                    backupCount++;
                }
            }


            restoreBackupPanel.ResetNavigationData();
            return backupCount;
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
