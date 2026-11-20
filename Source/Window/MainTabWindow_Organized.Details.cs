using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchOrganized2
{
    public partial class MainTabWindow_Organized
    {
        private const float StartButtonHeight = 48f;
        private const float DevButtonHeight = 26f;

        // Vanilla's own project info, requirements and start button, drawn inside the themed panel. Reusing them keeps
        // every requirement, link and rule vanilla has, and every other mod's patches to them (Semi Random Research's
        // start-button lock among them); the start button and divider are themed by ResearchWindowTheme.
        private delegate void PrimaryInfoDelegate(MainTabWindow_Research window, Rect rect, ref float y);
        private delegate void RectDelegate(MainTabWindow_Research window, Rect rect);

        private static readonly PrimaryInfoDelegate drawPrimaryInfo =
            AccessTools.MethodDelegate<PrimaryInfoDelegate>(AccessTools.Method(typeof(MainTabWindow_Research), "DrawProjectPrimaryInfo"));
        private static readonly RectDelegate drawScrollView =
            AccessTools.MethodDelegate<RectDelegate>(AccessTools.Method(typeof(MainTabWindow_Research), "DrawProjectScrollView"));
        private static readonly RectDelegate drawStartButton =
            AccessTools.MethodDelegate<RectDelegate>(AccessTools.Method(typeof(MainTabWindow_Research), "DrawStartButton"));

        private static Texture2D detailsIcon;
        private Vector2 tabInfoScroll;
        private float tabInfoHeight;

        private void DrawDetailsPanel(Rect rect)
        {
            detailsIcon = detailsIcon ?? CoreWidgets.Icon("info");
            var settings = ResearchOrganizedMod.settings;
            bool folded = settings.detailsFolded;
            bool open = SidePanel.Begin(rect, TranslationCache.Get("ResearchOrganized2_Details"), detailsIcon, ref folded, SidePanel.Side.Right,
                out Rect inner, out _, out _);
            if (folded != settings.detailsFolded)
            {
                settings.detailsFolded = folded;
                settings.Write();
            }
            if (!open) return;

            var project = Selected;
            if (project == null)
            {
                DrawTabInfo(inner);
                return;
            }

            Widgets.BeginGroup(inner);
            try
            {
                Rect area = inner.AtZero();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                Rect start = new Rect(area.x, area.yMax - StartButtonHeight, area.width, StartButtonHeight);
                float bottom = start.y - Gap;
                if (Prefs.DevMode) bottom = DrawDevButtons(new Rect(area.x, bottom - DevButtonHeight, area.width, DevButtonHeight), project) - Gap;

                float y = 0f;
                drawPrimaryInfo?.Invoke(this, area, ref y);
                Rect scroll = new Rect(area.x, y, area.width, Mathf.Max(0f, bottom - y));
                drawScrollView?.Invoke(this, scroll);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                drawStartButton?.Invoke(this, start);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Widgets.EndGroup();
            }
        }

        /// <summary>The open tab's title and description, shown while no project is selected.</summary>
        private void DrawTabInfo(Rect rect)
        {
            if (CurTab == null) return;
            Rect view = Theme.BeginScroll(rect, ref tabInfoScroll, tabInfoHeight);
            float y = 0f;
            string title = CurTab.generalTitle.CapitalizeFirst();
            Theme.Label(new Rect(0f, y, view.width, 32f), title, TextAnchor.MiddleLeft, Color.white, GameFont.Medium);
            y += 36f;
            string description = CurTab.generalDescription ?? "";
            Text.Font = GameFont.Small;
            float height = Text.CalcHeight(description, view.width);
            Theme.Label(new Rect(0f, y, view.width, height), description, TextAnchor.UpperLeft, Theme.TextDim);
            y += height;
            tabInfoHeight = y;
            Theme.EndScroll();
        }

        /// <summary>Dev-mode finish and techprint buttons, as vanilla offers them; returns their top edge.</summary>
        private float DrawDevButtons(Rect row, ResearchProjectDef project)
        {
            float half = (row.width - Gap) / 2f;
            Rect finish = new Rect(row.x, row.y, half, row.height);
            Rect techprint = new Rect(finish.xMax + Gap, row.y, half, row.height);
            if (!Find.ResearchManager.IsCurrentProject(project) && !project.IsFinished
                && CoreWidgets.Button(finish, "Debug: Finish now", null, Theme.HighlightGray))
            {
                Find.ResearchManager.SetCurrentProject(project);
                Find.ResearchManager.FinishProject(project);
            }
            if (!project.TechprintRequirementMet && CoreWidgets.Button(techprint, "Debug: Apply techprint", null, Theme.HighlightGray))
            {
                Find.ResearchManager.ApplyTechprint(project, null);
                SoundDefOf.TechprintApplied.PlayOneShotOnCamera();
            }
            return row.y;
        }

        // Active project bar.

        private static Texture2D activeIcon;
        private string noProject;
        private string basicLabel;
        private string advancedLabel;
        private ResearchProjectDef[] progressFor = new ResearchProjectDef[2];
        private readonly int[] progressShown = { -1, -1 };
        private readonly string[] progressText = new string[2];

        private void DrawActiveBar(Rect rect)
        {
            activeIcon = activeIcon ?? CoreWidgets.Icon("flask");
            if (Event.current.type == EventType.Repaint)
            {
                Rounded.BeginBatch();
                try
                {
                    Rounded.Fill(rect, Theme.BG);
                    Rounded.Border(rect, Color.Lerp(Theme.BGDark, Theme.HighlightGray, 0.35f));
                }
                finally
                {
                    Rounded.EndBatch();
                }
            }
            noProject = noProject ?? "NoProjectSelected".Translate().ToString();
            Rect inner = rect.ContractedBy(6f, 4f);
            if (ModsConfig.AnomalyActive && CurTab == ResearchTabDefOf.Anomaly)
            {
                basicLabel = basicLabel ?? KnowledgeCategoryDefOf.Basic.LabelCap.ToString();
                advancedLabel = advancedLabel ?? KnowledgeCategoryDefOf.Advanced.LabelCap.ToString();
                float half = (inner.width - Gap * 2f) / 2f;
                DrawActiveSlot(new Rect(inner.x, inner.y, half, inner.height), 0, Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Basic), basicLabel);
                DrawActiveSlot(new Rect(inner.xMax - half, inner.y, half, inner.height), 1, Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Advanced), advancedLabel);
            }
            else
            {
                DrawActiveSlot(inner, 0, Find.ResearchManager.GetProject(), null);
            }
        }

        private void DrawActiveSlot(Rect rect, int slot, ResearchProjectDef project, string prefix)
        {
            CoreWidgets.DrawIcon(new Rect(rect.x, rect.center.y - 10f, 20f, 20f), activeIcon, project != null ? Theme.HighlightYellow : Theme.TextFaint);
            float x = rect.x + 28f;
            if (prefix != null)
            {
                Text.Font = GameFont.Small;
                float width = Text.CalcSize(prefix).x + 8f;
                CoreWidgets.Line(new Rect(x, rect.y, width, rect.height), prefix, Theme.TextDim);
                x += width;
            }
            if (project == null)
            {
                CoreWidgets.Line(new Rect(x, rect.y, rect.xMax - x, rect.height), noProject, Theme.TextFaint);
                return;
            }

            var info = Info(project);
            float labelWidth = Mathf.Min(260f, (rect.xMax - x) * 0.35f);
            Rect label = new Rect(x, rect.y, labelWidth, rect.height);
            CoreWidgets.Line(label, info.label, Color.white);
            if (Widgets.ButtonInvisible(label)) SelectProject(project, true);

            int shown = Mathf.FloorToInt(project.ProgressApparent);
            if (progressFor[slot] != project || progressShown[slot] != shown)
            {
                progressFor[slot] = project;
                progressShown[slot] = shown;
                progressText[slot] = project.ProgressApparentString + " / " + info.cost;
            }
            Text.Font = GameFont.Small;
            float textWidth = Text.CalcSize(progressText[slot]).x + 8f;
            Rect text = new Rect(rect.xMax - textWidth, rect.y, textWidth, rect.height);
            Rect bar = new Rect(label.xMax + Gap, rect.center.y - 6f, Mathf.Max(0f, text.x - Gap - label.xMax - Gap), 12f);
            Theme.Bar(bar, project.ProgressPercent, Theme.HighlightYellow);
            CoreWidgets.Line(text, progressText[slot], Theme.TextDim, GameFont.Small, TextAnchor.MiddleRight);
        }
    }
}
