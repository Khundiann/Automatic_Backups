using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if MONO
using ScheduleOne;
using ScheduleOne.UI.MainMenu;
using ScheduleOne.UI.Settings;
using TMPro;
#else
using Il2CppScheduleOne;
using Il2CppTMPro;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI.Settings;
#endif

namespace AutomaticBackups
{
    // Allows us to add a category to the Settings display with our mod's settings
    // We need a MonoBehavior so we can clone existing objects in the scene hierarchy (I don't know how to do this outside of a MonoBehavior)
    [RegisterTypeInIl2Cpp]
    public class SettingsMenuMod : MonoBehaviour
    {
        const string SettingsObjectName = "Settings";
        const string BackupsCategoryName = "Backups";
        const string SettingsRowsPath = "ControlsPanel/Scroll View/Viewport/Vertical Layout";
        const string DeleteOldSavesRowName = "DeleteOldSaves";
        const string RetainedCountRowName = "RetainedCount";
        const string EnableAutoSaveRowName = "Enable AutoSave";
        const string AutoSaveTimeRowName = "AutoSave Time";
        const string ToggleLabelName = "Option Name - Text (TMP)";
        const string SliderLabelName = "Label";
        const string ToggleControlName = "Toggle (OffOn)";
        const string SliderControlName = "Slider";

        void Awake()
        {
            Init(); // Calling the Awake code from another function will keep VS from making all the code gray
        }

        void Init()
        {
            // We should be attached to the MainMenu GameObject
            GameObject mainMenu = gameObject;

            // Settings object will be a child of MainMenu
            Transform settings = mainMenu.transform.Find(SettingsObjectName);
            if (settings == null)
            {
                Core.Log("Failed to create the Backups settings: MainMenu/Settings was not found.");
                return;
            }

            SettingsScreen settingsScreen = settings.GetComponent<SettingsScreen>();
            if (settingsScreen == null)
            {
                Core.Log("Failed to create the Backups settings: MainMenu/Settings has no SettingsScreen component.");
                return;
            }

            SettingsScreen.SettingsCategory controlsCategory = FindControlsCategory(settingsScreen);
            if (controlsCategory == null)
                return;

            // Create the Backups button (tab on the settings display)
            Toggle backupsButton = CreateBackupsButton(settingsScreen, controlsCategory);
            if (backupsButton == null)
                return;

            // Create the panel to display our settings
            GameObject backupsPanel = CreateBackupsPanel(settingsScreen, controlsCategory);
            if (backupsPanel == null)
            {
                Destroy(backupsButton.gameObject);
                return;
            }

            // Wire up the Button and panel on the settings display
            AddSettingsCategory(settingsScreen, backupsButton, backupsPanel);
        }

        // Locate the game's Controls category through its serialized references instead
        // of relying on a particular hierarchy beneath the Settings object.
        SettingsScreen.SettingsCategory FindControlsCategory(SettingsScreen settingsScreen)
        {
            var categories = settingsScreen.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                SettingsScreen.SettingsCategory category = categories[i];
                if (category == null)
                    continue;

                string toggleName = category.Toggle != null ? category.Toggle.gameObject.name : "";
                string panelName = category.Panel != null ? category.Panel.name : "";
                if (toggleName.IndexOf("Control", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    panelName.IndexOf("Control", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (category.Toggle == null || category.Panel == null)
                    {
                        Core.Log("Failed to create the Backups settings: the Controls category is missing its Toggle or Panel reference.");
                        return null;
                    }

                    return category;
                }
            }

            Core.Log("Failed to create the Backups settings: no Controls category was found in SettingsScreen.Categories.");
            return null;
        }

        // Create a button (tab) in the Settings display for our Backups category
        Toggle CreateBackupsButton(SettingsScreen settingsScreen, SettingsScreen.SettingsCategory controlsCategory)
        {
            Transform controlsButton = controlsCategory.Toggle.transform;
            Transform buttons = controlsButton.parent;
            if (buttons == null)
            {
                Core.Log("Failed to create the Backups settings: the Controls toggle has no parent.");
                return null;
            }

            // Clone the Controls button and update the clone for our Backup category button.
            Transform modSettingsButton = Instantiate(controlsButton, buttons);
            modSettingsButton.name = BackupsCategoryName;

            // The label object was named "Name" in older game versions. Find the text
            // component recursively so this also works with the current settings-tab prefab.
            TextMeshProUGUI nameUGUI = modSettingsButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameUGUI == null)
            {
                Core.Log("Failed to create the Backups settings: the cloned Controls tab has no text label.");
                Destroy(modSettingsButton.gameObject);
                return null;
            }
            nameUGUI.text = "Backups";

            Toggle toggle = modSettingsButton.GetComponent<Toggle>();
            if (toggle == null)
            {
                Core.Log("Failed to create the Backups settings: the cloned Controls tab has no Toggle component.");
                Destroy(modSettingsButton.gameObject);
                return null;
            }

            // The cloned toggle can retain the Controls category callback. Remove it so
            // selecting Backups cannot activate both panels, and start the new tab off.
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(false);

            if (!RegisterTabNavigation(settingsScreen.transform, controlsButton, modSettingsButton))
            {
                Destroy(modSettingsButton.gameObject);
                return null;
            }

            return toggle;
        }

        // Runtime clones do not retain UISelectable's non-serialized ParentPanel.
        // Register the new tab with the same panel as the native Controls tab so
        // pointer, keyboard, and controller navigation all use the game UI system.
        bool RegisterTabNavigation(Transform settings, Transform controlsButton, Transform backupsButton)
        {
            UISelectable controlsSelectable = controlsButton.GetComponent<UISelectable>();
            UISelectable backupsSelectable = backupsButton.GetComponent<UISelectable>();
            if (controlsSelectable == null || backupsSelectable == null)
            {
                Core.Log("Failed to register Backups tab navigation: a UISelectable component was not found.");
                return false;
            }

            if (backupsSelectable.ParentPanel != null)
                return true;

            // UIPanel assigns ParentPanel from its serialized selectable list during
            // Awake. The Settings hierarchy is inactive here, so locate the panel by
            // that list instead of reading the not-yet-initialized ParentPanel field.
            UIPanel[] panels = settings.GetComponentsInChildren<UIPanel>(true);
            for (int i = 0; i < panels.Length; i++)
            {
                var selectables = panels[i].Selectables;
#if MONO
                int selectableCount = selectables.Count;
#else
                int selectableCount = selectables
                    .Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<UISelectable>>()
                    .Count;
#endif
                for (int j = 0; j < selectableCount; j++)
                {
                    UISelectable selectable = selectables[j];
                    if (selectable != controlsSelectable)
                        continue;

                    if (!panels[i].AddSelectable(backupsSelectable))
                    {
                        Core.Log("Failed to register Backups tab navigation: the tab could not be added to the Controls UIPanel.");
                        return false;
                    }

                    return true;
                }
            }

            Core.Log("Failed to register Backups tab navigation: the Controls tab's UIPanel was not found.");
            return false;
        }

        // Create a panel to display our mod's settings
        GameObject CreateBackupsPanel(SettingsScreen settingsScreen, SettingsScreen.SettingsCategory controlsCategory)
        {
            Transform controlsPanel = controlsCategory.Panel.transform;
            Transform panelsParent = controlsPanel.parent;
            if (panelsParent == null)
            {
                Core.Log("Failed to create the Backups settings: the Controls panel has no parent.");
                return null;
            }

            // Clone the Controls panel
            Transform modSettingsPanel = Instantiate(controlsPanel, panelsParent);

            // Update the panel clone
            modSettingsPanel.name = BackupsCategoryName;

            // Configure replacement components while inactive. SettingsSlider.Awake()
            // expects its serialized references to exist as soon as it runs.
            modSettingsPanel.gameObject.SetActive(false);

            // Setup auto-delete controls
            Transform retentionSlider = SetupRetentionSlider(modSettingsPanel);
            if (retentionSlider == null)
            {
                Destroy(modSettingsPanel.gameObject);
                return null;
            }

            // RetainedCount is a complete row in the current hierarchy, so its parent
            // identifies the container that owns all Controls setting rows.
            Transform rowsParent = retentionSlider.parent;
            Transform deleteOldestToggle = SetupDeleteOldestToggle(modSettingsPanel, rowsParent, retentionSlider);
            if (rowsParent == null || deleteOldestToggle == null ||
                deleteOldestToggle.parent != rowsParent || retentionSlider.parent != rowsParent ||
                deleteOldestToggle == retentionSlider)
            {
                Core.Log("Failed to create the Backups settings: the cloned setting rows have an unexpected hierarchy.");
                Destroy(modSettingsPanel.gameObject);
                return null;
            }

            DestroyAllChildrenExcept(rowsParent, deleteOldestToggle, retentionSlider);
            deleteOldestToggle.SetSiblingIndex(0);
            retentionSlider.SetSiblingIndex(1);

            // Setup auto-save controls by cloning auto-delete controls
            if (!AddAutoSaveControls(rowsParent, deleteOldestToggle, retentionSlider))
            {
                Destroy(modSettingsPanel.gameObject);
                return null;
            }

            if (!RegisterPanelNavigation(settingsScreen.transform, controlsPanel, modSettingsPanel))
            {
                Destroy(modSettingsPanel.gameObject);
                return null;
            }

            return modSettingsPanel.gameObject;
        }

        // Like selectable ownership, screen ownership is assigned from a serialized
        // list during Awake. Add the runtime-cloned panel to the screen that owns the
        // native Controls panel before the inactive Settings hierarchy is activated.
        bool RegisterPanelNavigation(Transform settings, Transform controlsPanel, Transform backupsPanel)
        {
            UIPanel controlsUiPanel = controlsPanel.GetComponentInChildren<UIPanel>(true);
            UIPanel backupsUiPanel = backupsPanel.GetComponentInChildren<UIPanel>(true);
            if (controlsUiPanel == null || backupsUiPanel == null)
            {
                Core.Log("Failed to register Backups panel navigation: a UIPanel component was not found.");
                return false;
            }

            // Rows cloned at runtime are not present in the panel's serialized
            // selectable list. Register the completed hierarchy explicitly so every
            // retained and cloned control receives this panel as its ParentPanel.
            UISelectable[] backupsSelectables = backupsUiPanel.GetComponentsInChildren<UISelectable>(true);
            for (int i = 0; i < backupsSelectables.Length; i++)
                backupsUiPanel.AddSelectable(backupsSelectables[i]);

            if (backupsUiPanel.ParentScreen != null)
                return true;

            UIScreen[] screens = settings.GetComponentsInChildren<UIScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                var panels = screens[i].Panels;
#if MONO
                int panelCount = panels.Count;
#else
                int panelCount = panels
                    .Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<UIPanel>>()
                    .Count;
#endif
                for (int j = 0; j < panelCount; j++)
                {
                    if (panels[j] != controlsUiPanel)
                        continue;

                    screens[i].AddPanel(backupsUiPanel);
                    return true;
                }
            }

            Core.Log("Failed to register Backups panel navigation: the Controls panel's UIScreen was not found.");
            return false;
        }

        // Finds the "invertY" toggle we cloned from the Controls panel and repurposes it to toggle the option to delete oldest saves
        Transform SetupDeleteOldestToggle(Transform settingsPanel, Transform rowsParent, Transform retentionSlider)
        {
            if (rowsParent == null)
            {
                Core.Log("Failed to create the Backups settings: the settings rows have no parent container.");
                return null;
            }

            InvertYToggle invertYToggle = settingsPanel.GetComponentInChildren<InvertYToggle>(true);
            if (invertYToggle == null)
            {
                Core.Log("Failed to create the Backups settings: no InvertYToggle was found in the Controls panel.");
                return null;
            }

            Transform toggle = invertYToggle.transform;
            Transform toggleRow = GetDirectChild(rowsParent, toggle);
            if (toggleRow == null)
            {
                Core.Log("Failed to create the Backups settings: the InvertYToggle is not inside the settings rows container.");
                return null;
            }

            UIToggle uiToggle = FindUiToggle(toggleRow);
            if (uiToggle == null)
            {
                Core.Log("Failed to create the Backups settings: InvertYToggle has no UIToggle reference.");
                return null;
            }

            toggleRow.name = DeleteOldSavesRowName;
            SetLabelText(toggleRow, "Limit Backups");
            uiToggle.SetStateWithoutNotify(Core.enableAutoDelete.Value);
            uiToggle.OnChanged.RemoveAllListeners();

            // Replace the InvertYToggle component with our DeleteOldestToggle component
            DestroyImmediate(invertYToggle);
            DeleteOldestToggle deleteOldestToggle = toggle.gameObject.AddComponent<DeleteOldestToggle>();
            deleteOldestToggle.Configure(uiToggle, retentionSlider.gameObject);

            return toggleRow;
        }

        // Finds the mouse sensitivity slider we cloned from the Controls panel and repurposes it to control the number of files to back up
        Transform SetupRetentionSlider(Transform settingsPanel)
        {
            SettingsSlider sensitivitySlider = settingsPanel.GetComponentInChildren<SensitivitySlider>(true);
#if IL2CPP
            if (sensitivitySlider == null)
                sensitivitySlider = settingsPanel.GetComponentInChildren<PointerSensitivitySlider>(true);
#endif
            if (sensitivitySlider == null)
            {
                Core.Log("Failed to create the Backups settings: no sensitivity SettingsSlider was found in the Controls panel.");
                return null;
            }

            Transform slider = sensitivitySlider.transform;
            Transform sliderParent = slider.parent;
            if (sliderParent == null)
            {
                Core.Log("Failed to create the Backups settings: the sensitivity slider has no parent.");
                return null;
            }

            // Remove the listener that the SensitivitySlider will have added
            Slider sliderComponent = slider.GetComponent<Slider>();
            if (sliderComponent == null)
            {
                Core.Log("Failed to create the Backups settings: the sensitivity slider object has no UI Slider component.");
                return null;
            }
            sliderComponent.onValueChanged.RemoveAllListeners();

            TextMeshProUGUI valueLabel = FindSliderValueLabel(sliderParent);
            bool displayValue = sensitivitySlider.DisplayValue;
            float valueDisplayTime = sensitivitySlider.ValueDisplayTime;

            sliderParent.name = RetainedCountRowName;
            SetLabelText(sliderParent, "Max Files Per Save Slot");

            // Replace the SensitivitySlider with our RetentionAmountSlider
            DestroyImmediate(sensitivitySlider);
            RetentionAmountSlider retentionAmountSlider = slider.gameObject.AddComponent<RetentionAmountSlider>();
            retentionAmountSlider.Configure(sliderComponent, valueLabel, displayValue, valueDisplayTime);

            return sliderParent;
        }

        // Update a setting row's title through the names used by the old and current
        // settings prefabs, without depending on backend-specific protected fields.
        void SetLabelText(Transform parent, string text)
        {
            Transform label = FindSettingLabel(parent);
            TMP_Text labelText = label != null ? label.GetComponent<TMP_Text>() : null;

            if (labelText == null)
            {
                Core.Log($"Failed to update the label for {parent.name}: no text component was found.");
                return;
            }

            labelText.text = text;
        }

        Transform FindSettingLabel(Transform row)
        {
            Transform label = row.Find(ToggleLabelName);
            return label != null ? label : row.Find(SliderLabelName);
        }

        UIToggle FindUiToggle(Transform row)
        {
            Transform control = row.Find(ToggleControlName);
            UIToggle toggle = control != null ? control.GetComponent<UIToggle>() : null;
            return toggle != null ? toggle : row.GetComponentInChildren<UIToggle>(true);
        }

        TextMeshProUGUI FindSliderValueLabel(Transform row)
        {
            Transform title = FindSettingLabel(row);
            TMP_Text titleText = title != null ? title.GetComponent<TMP_Text>() : null;
            TextMeshProUGUI[] textComponents = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < textComponents.Length; i++)
            {
                TextMeshProUGUI candidate = textComponents[i];
                if (candidate != null && candidate != titleText)
                    return candidate;
            }

            return null;
        }

        // The cloned title text is restored from the Controls panel when its TMP
        // component is first enabled. Apply the custom titles after activation.
        void ApplyActivePanelLabels(Transform settingsPanel)
        {
            Transform rows = settingsPanel.Find(SettingsRowsPath);
            if (rows == null)
            {
                Core.Log("Failed to update the active Backups labels: the settings rows container was not found.");
                return;
            }

            SetActiveLabel(rows, DeleteOldSavesRowName + "/" + ToggleLabelName, "Limit Backups");
            SetActiveLabel(rows, RetainedCountRowName + "/" + SliderLabelName, "Max Files Per Save Slot");
            SetActiveLabel(rows, EnableAutoSaveRowName + "/" + ToggleLabelName, "Enable AutoSave");
            SetActiveLabel(rows, AutoSaveTimeRowName + "/" + SliderLabelName, "Minutes Before Saving");
        }

        void SetActiveLabel(Transform rows, string relativePath, string text)
        {
            Transform label = rows.Find(relativePath);
            TMP_Text labelText = label != null ? label.GetComponent<TMP_Text>() : null;
            if (labelText == null)
            {
                Core.Log($"Failed to update the active Backups label at '{relativePath}'.");
                return;
            }

            labelText.text = text;
        }

        // Keep every setting control in one responsive column. The cloned Controls
        // rows use a fixed column sized for their original, shorter labels, so base
        // the new column on the widest rendered Backups label instead.
        void ApplyActivePanelControlColumn(Transform settingsPanel)
        {
            Transform rows = settingsPanel.Find(SettingsRowsPath);
            if (rows == null)
            {
                Core.Log("Failed to align the Backups controls: the settings rows container was not found.");
                return;
            }

            string[] rowNames =
            {
                DeleteOldSavesRowName,
                RetainedCountRowName,
                EnableAutoSaveRowName,
                AutoSaveTimeRowName
            };
            string[] labelNames =
            {
                ToggleLabelName,
                SliderLabelName,
                ToggleLabelName,
                SliderLabelName
            };
            string[] controlNames =
            {
                ToggleControlName,
                SliderControlName,
                ToggleControlName,
                SliderControlName
            };

            RectTransform[] rowRects = new RectTransform[rowNames.Length];
            RectTransform[] controlRects = new RectTransform[rowNames.Length];
            float widestLabelRight = float.NegativeInfinity;
            float nativeControlLeft = float.NegativeInfinity;
            float maximumControlLeft = float.PositiveInfinity;

            for (int i = 0; i < rowNames.Length; i++)
            {
                Transform row = rows.Find(rowNames[i]);
                Transform label = row != null ? row.Find(labelNames[i]) : null;
                Transform control = row != null ? row.Find(controlNames[i]) : null;
                TMP_Text labelText = label != null ? label.GetComponent<TMP_Text>() : null;
                RectTransform rowRect = row != null ? row.GetComponent<RectTransform>() : null;
                RectTransform controlRect = control != null ? control.GetComponent<RectTransform>() : null;

                if (labelText == null || rowRect == null || controlRect == null)
                {
                    Core.Log($"Failed to align the Backups controls: row '{rowNames[i]}' has an unexpected layout.");
                    return;
                }

                rowRects[i] = rowRect;
                controlRects[i] = controlRect;

                RectTransform labelRect = labelText.rectTransform;
                Vector3 renderedLabelRight = labelRect.TransformPoint(
                    new Vector3(labelRect.rect.xMin + labelText.preferredWidth, 0f, 0f));
                float labelRightInRow = row.InverseTransformPoint(renderedLabelRight).x;
                widestLabelRight = Mathf.Max(widestLabelRight, labelRightInRow);

                GetHorizontalBoundsInAncestor(controlRect, row, out float controlLeft, out float controlRight);
                float controlWidth = controlRight - controlLeft;
                nativeControlLeft = Mathf.Max(nativeControlLeft, controlLeft);
                maximumControlLeft = Mathf.Min(
                    maximumControlLeft,
                    rowRect.rect.xMax - controlWidth);
            }

            const float labelControlGap = 24f;
            float controlColumn = Mathf.Max(nativeControlLeft, widestLabelRight + labelControlGap);
            if (maximumControlLeft >= nativeControlLeft)
                controlColumn = Mathf.Min(controlColumn, maximumControlLeft);

            for (int i = 0; i < rowNames.Length; i++)
            {
                Transform row = rowRects[i].transform;
                RectTransform controlRect = controlRects[i];
                GetHorizontalBoundsInAncestor(controlRect, row, out float controlLeft, out _);
                float horizontalAdjustment = controlColumn - controlLeft;
                controlRect.anchoredPosition += new Vector2(horizontalAdjustment, 0f);
            }
        }

        // RectTransformUtility.CalculateRelativeRectTransformBounds is stripped from
        // the current IL2CPP player. Transform the two horizontal edges manually.
        void GetHorizontalBoundsInAncestor(
            RectTransform target,
            Transform ancestor,
            out float minimumX,
            out float maximumX)
        {
            Vector3 leftWorld = target.TransformPoint(new Vector3(target.rect.xMin, 0f, 0f));
            Vector3 rightWorld = target.TransformPoint(new Vector3(target.rect.xMax, 0f, 0f));
            float left = ancestor.InverseTransformPoint(leftWorld).x;
            float right = ancestor.InverseTransformPoint(rightWorld).x;
            minimumX = Mathf.Min(left, right);
            maximumX = Mathf.Max(left, right);
        }

        // Destroys all children of a given Transform
        public static void DestroyAllChildren(Transform parent)
        {
            // Iterate backwards to avoid issues while modifying the hierarchy
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }

        // Return the direct branch beneath ancestor that contains descendant.
        Transform GetDirectChild(Transform ancestor, Transform descendant)
        {
            if (ancestor == null || descendant == null || ancestor == descendant)
                return null;

            Transform current = descendant;
            while (current != null && current.parent != ancestor)
                current = current.parent;

            return current != null && current.parent == ancestor ? current : null;
        }

        // Destroy unwanted cloned rows immediately so they cannot take part in a
        // layout pass before Unity reaches the end of the frame.
        void DestroyAllChildrenExcept(Transform parent, Transform first, Transform second)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != first && child != second)
                    DestroyImmediate(child.gameObject);
            }
        }

        bool AddAutoSaveControls(Transform rowsParent, Transform deleteOldestToggle, Transform retentionSlider)
        {
            // Setup auto-save controls by cloning auto-delete controls
            Transform autoSaveToggle = Instantiate(deleteOldestToggle, rowsParent);
            Transform autoSaveTimeSlider = Instantiate(retentionSlider, rowsParent);
            autoSaveToggle.name = EnableAutoSaveRowName;
            autoSaveTimeSlider.name = AutoSaveTimeRowName;

            // Update the autosave controls
            DeleteOldestToggle deleteOldestComponent = autoSaveToggle.GetComponentInChildren<DeleteOldestToggle>(true);
            RetentionAmountSlider retentionComponent = autoSaveTimeSlider.GetComponentInChildren<RetentionAmountSlider>(true);
            if (deleteOldestComponent == null || retentionComponent == null)
            {
                Core.Log("Failed to create the auto-save settings: cloned controls are missing their mod components.");
                Destroy(autoSaveToggle.gameObject);
                Destroy(autoSaveTimeSlider.gameObject);
                return false;
            }

            GameObject toggleObject = deleteOldestComponent.gameObject;
            GameObject sliderObject = retentionComponent.gameObject;
            UIToggle autoSaveUiToggle = FindUiToggle(autoSaveToggle);
            Slider autoSaveSlider = retentionComponent.SliderReference;
            TextMeshProUGUI autoSaveValueLabel = retentionComponent.ValueLabelReference;
            bool autoSaveDisplayValue = retentionComponent.DisplayValueReference;
            float autoSaveValueDisplayTime = retentionComponent.ValueDisplayTimeReference;
            DestroyImmediate(deleteOldestComponent);
            DestroyImmediate(retentionComponent);
            SetLabelText(autoSaveToggle, "Enable AutoSave");
            SetLabelText(autoSaveTimeSlider, "Minutes Before Saving");
            AutoSaveToggle toggleComponent = toggleObject.AddComponent<AutoSaveToggle>();
            toggleComponent.Configure(autoSaveUiToggle, autoSaveTimeSlider.gameObject);
            AutoSaveTimeSlider autoSaveTimeComponent = sliderObject.AddComponent<AutoSaveTimeSlider>();
            autoSaveTimeComponent.Configure(autoSaveSlider, autoSaveValueLabel, autoSaveDisplayValue, autoSaveValueDisplayTime);
            return true;
        }

        // Add a tab to the settings display with our mod's settings
        void AddSettingsCategory(SettingsScreen settingsScreen, Toggle modSettingsButton, GameObject modSettingsPanel)
        {
            // Embiggen the Categories array
            EnlargeCategories(settingsScreen);

            // Add a new settings category with our button & panel
            SettingsScreen.SettingsCategory backupCategory = new SettingsScreen.SettingsCategory();
            backupCategory.Toggle = modSettingsButton;
            backupCategory.Panel = modSettingsPanel;
            int categoryIndex = settingsScreen.Categories.Length - 1;
            settingsScreen.Categories[categoryIndex] = backupCategory;

            // SettingsScreen may already have initialized before this runtime category
            // was added, so wire the new tab explicitly.
#if MONO
            modSettingsButton.onValueChanged.AddListener(new UnityAction<bool>((on) =>
#else
            modSettingsButton.onValueChanged.AddListener(new Action<bool>((on) =>
#endif
            {
                if (on)
                {
                    settingsScreen.ShowCategory(categoryIndex);
                    ApplyActivePanelLabels(modSettingsPanel.transform);
                    Canvas.ForceUpdateCanvases();
                    ApplyActivePanelControlColumn(modSettingsPanel.transform);
                }
            }));

            modSettingsPanel.SetActive(false);
        }

        // Copy the Categories array to a new array with one more slot and assign it back to the SettingsScreen.
        // (This proved simpler than figuring out how to use Array.Resize() with Il2Cpp.)
        void EnlargeCategories(SettingsScreen settingsScreen)
        {
            var categories = settingsScreen.Categories;

            // Create a larger array
            SettingsScreen.SettingsCategory[] newCategories = new SettingsScreen.SettingsCategory[categories.Length + 1];

            // Copy the old array to the new one
            for (int i = 0; i < categories.Length; i++)
                newCategories[i] = categories[i];

            settingsScreen.Categories = newCategories;
        }
    }
}
