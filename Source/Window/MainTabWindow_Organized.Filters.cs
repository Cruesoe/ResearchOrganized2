using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResearchOrganized2
{
    public partial class MainTabWindow_Organized
    {
        private const float FilterBarHeight = 30f;
        private const float ChipWidth = 130f;
        private const string SearchControl = "ResearchOrganizedTwoSearchField";

        private string searchText = "";

        /// <summary>Projects matching the search, by name or by anything they unlock; null while the search is empty.</summary>
        private HashSet<ResearchProjectDef> searchMatches;

        private static Texture2D lockIcon;
        private static Texture2D checkIcon;
        private static Texture2D futureIcon;
        private static Texture2D zoomInIcon;
        private static Texture2D zoomOutIcon;
        private static Texture2D fitIcon;

        private void DrawSearch(Rect rect)
        {
            if (SearchField.Draw(rect, ref searchText, SearchControl, TranslationCache.Get("ResearchOrganized2_SearchHint")))
                UpdateSearch();
        }

        private void UpdateSearch()
        {
            string text = searchText?.Trim();
            if (text.NullOrEmpty())
            {
                searchMatches = null;
                return;
            }
            searchMatches = searchMatches ?? new HashSet<ResearchProjectDef>();
            searchMatches.Clear();
            foreach (var project in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (Matches(project.LabelCap, text)) { searchMatches.Add(project); continue; }
                var unlocked = project.UnlockedDefs;
                for (int i = 0; i < unlocked.Count; i++)
                {
                    if (Matches(unlocked[i].LabelCap, text)) { searchMatches.Add(project); break; }
                }
            }
        }

        private static bool Matches(string label, string text) =>
            !label.NullOrEmpty() && label.IndexOf(text, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Show/hide chips for the visibility filters, remembered in the settings, and the zoom controls.</summary>
        private void DrawFilterBar(Rect rect)
        {
            if (lockIcon == null)
            {
                lockIcon = CoreWidgets.Icon("lock");
                checkIcon = CoreWidgets.Icon("check");
                futureIcon = CoreWidgets.Icon("hourglass");
                zoomInIcon = CoreWidgets.Icon("plus");
                zoomOutIcon = CoreWidgets.Icon("minus");
                fitIcon = CoreWidgets.Icon("frame-corners");
            }
            var settings = ResearchOrganizedMod.settings;
            float x = rect.x;
            if (FilterChip(ref x, rect, "ResearchOrganized2_FilterLocked", "ResearchOrganized2_FilterLockedTip", lockIcon, !settings.hideUnavailableProjects))
                SetFilter(ref settings.hideUnavailableProjects);
            if (FilterChip(ref x, rect, "ResearchOrganized2_FilterFinished", "ResearchOrganized2_FilterFinishedTip", checkIcon, !settings.hideFinishedProjects))
                SetFilter(ref settings.hideFinishedProjects);
            if (FilterChip(ref x, rect, "ResearchOrganized2_FilterFuture", "ResearchOrganized2_FilterFutureTip", futureIcon, !settings.hideFutureTechLevels))
                SetFilter(ref settings.hideFutureTechLevels);

            float right = rect.xMax;
            Rect fit = new Rect(right - 28f, rect.y + 1f, 28f, 28f);
            if (CoreWidgets.IconButton(fit, fitIcon, TranslationCache.Get("ResearchOrganized2_FitTip"))) FitNextDraw();
            Rect zoomIn = new Rect(fit.x - 32f, fit.y, 28f, 28f);
            if (CoreWidgets.IconButton(zoomIn, zoomInIcon, TranslationCache.Get("ResearchOrganized2_ZoomInTip"))) ZoomBy(1);
            Rect zoomLabel = new Rect(zoomIn.x - 52f, rect.y, 48f, rect.height);
            CoreWidgets.Line(zoomLabel, ZoomLabel, Theme.TextDim, GameFont.Small, TextAnchor.MiddleCenter);
            Rect zoomOut = new Rect(zoomLabel.x - 32f, fit.y, 28f, 28f);
            if (CoreWidgets.IconButton(zoomOut, zoomOutIcon, TranslationCache.Get("ResearchOrganized2_ZoomOutTip"))) ZoomBy(-1);
        }

        private static bool FilterChip(ref float x, Rect bar, string labelKey, string tipKey, Texture icon, bool shown)
        {
            Rect chip = new Rect(x, bar.y + 2f, ChipWidth, bar.height - 4f);
            x = chip.xMax + 6f;
            return CoreWidgets.Chip(chip, TranslationCache.Get(labelKey), shown ? ChipState.Include : ChipState.Off, Theme.HighlightGreen, -1,
                TranslationCache.Get(tipKey), icon);
        }

        private void SetFilter(ref bool hidden)
        {
            hidden = !hidden;
            ResearchOrganizedMod.settings.Write();
        }
    }
}
