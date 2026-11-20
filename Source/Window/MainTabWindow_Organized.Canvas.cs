using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ResearchOrganized2
{
    public partial class MainTabWindow_Organized
    {
        // Vanilla's research coordinates: pixels per unit of researchViewX and researchViewY, and its card width.
        private const float PixelsPerX = 190f;
        private const float PixelsPerY = 100f;
        private const float CardWidth = 140f;
        private const float CardHeight = 52f;
        private const float KnowledgeStrip = 14f;
        private const float CanvasMargin = 24f;
        private const int CurveSegments = 10;

        // Largest first, as Core's ZoomView expects: wheel down steps towards the end of the list.
        private static readonly float[] ZoomSteps = { 1.3f, 1.15f, 1f, 0.85f, 0.7f, 0.6f, 0.5f };

        /// <summary>Each tab's pan and zoom for this game; cleared when another game is loaded.</summary>
        private readonly Dictionary<ResearchTabDef, ZoomView> views = new Dictionary<ResearchTabDef, ZoomView>();
        private Game viewsGame;

        /// <summary>The current tab's visible projects and their bounds, rebuilt when the tab, the visible list or the layout changes.</summary>
        private readonly List<ResearchProjectDef> tabProjects = new List<ResearchProjectDef>();
        private readonly HashSet<ResearchProjectDef> tabProjectSet = new HashSet<ResearchProjectDef>();
        private ResearchTabDef projectsTab;
        private List<ResearchProjectDef> projectsSource;
        private int projectsLayout = -1;
        private Rect tabBounds;

        /// <summary>The selected project's prerequisites, all the way back, on the current tab.</summary>
        private readonly HashSet<ResearchProjectDef> chain = new HashSet<ResearchProjectDef>();
        private ResearchProjectDef chainFor;
        private ResearchTabDef chainTab;

        private bool fitPending;
        private ResearchProjectDef centerPending;
        private bool selectingHere;
        private ResearchProjectDef lastSelected;
        private readonly HashSet<ResearchTabDef> homedTabs = new HashSet<ResearchTabDef>();

        private string notDiscovered;

        private void OnOpenCanvas()
        {
            if (Current.Game != viewsGame)
            {
                views.Clear();
                homedTabs.Clear();
                viewsGame = Current.Game;
                centerPending = Selected;
            }
            else if (ResearchOrganizedMod.settings.openOnCurrentProject)
            {
                centerPending = Selected;
            }
            projectsSource = null;
            // Vanilla reselects the current project on every open; only the cases above bring it into view.
            lastSelected = Selected;
        }

        private void OnSelectionChanged() => chainFor = null;

        private void CenterOnNextDraw(ResearchProjectDef project) => centerPending = project;

        private void FitNextDraw() => fitPending = true;

        private ZoomView View(ResearchTabDef tab)
        {
            if (!views.TryGetValue(tab, out var view))
            {
                view = new ZoomView(ZoomSteps);
                views[tab] = view;
            }
            return view;
        }

        private string ZoomLabel
        {
            get
            {
                float zoom = CurTab != null && views.TryGetValue(CurTab, out var view) ? view.zoom : 1f;
                return ZoomPercent(zoom);
            }
        }

        private static readonly Dictionary<int, string> percentLabels = new Dictionary<int, string>();

        private static string ZoomPercent(float zoom)
        {
            int percent = Mathf.RoundToInt(zoom * 100f);
            if (!percentLabels.TryGetValue(percent, out string label)) percentLabels[percent] = label = percent + "%";
            return label;
        }

        private Rect lastCanvas;

        private void ZoomBy(int direction)
        {
            if (CurTab == null) return;
            var view = View(CurTab);
            int nearest = 0;
            for (int i = 1; i < ZoomSteps.Length; i++)
                if (Mathf.Abs(ZoomSteps[i] - view.zoom) < Mathf.Abs(ZoomSteps[nearest] - view.zoom)) nearest = i;
            int next = Mathf.Clamp(nearest - direction, 0, ZoomSteps.Length - 1);
            Rect local = lastCanvas.AtZero();
            Vector2 center = view.ToContent(local.center);
            view.Home(local.center - center * ZoomSteps[next], ZoomSteps[next]);
        }

        private static float PosX(ResearchProjectDef project) => project.ResearchViewX * PixelsPerX;
        private static float PosY(ResearchProjectDef project) => project.ResearchViewY * PixelsPerY;

        private static float ExtraWidth(ResearchProjectDef project) =>
            ModsConfig.AnomalyActive && project.knowledgeCategory != null ? KnowledgeStrip : 0f;

        private static Rect CardRect(ResearchProjectDef project) =>
            new Rect(PosX(project), PosY(project), CardWidth + ExtraWidth(project), CardHeight);

        private void RefreshTabProjects()
        {
            var visible = VisibleResearchProjects;
            int layout = ResearchOrganizedMain.LayoutVersion;
            if (projectsTab == CurTab && ReferenceEquals(projectsSource, visible) && projectsLayout == layout) return;
            projectsTab = CurTab;
            projectsSource = visible;
            projectsLayout = layout;
            chainFor = null;
            tabProjects.Clear();
            tabProjectSet.Clear();
            bool any = false;
            Rect bounds = default;
            foreach (var project in visible)
            {
                if (project.tab != CurTab) continue;
                tabProjects.Add(project);
                tabProjectSet.Add(project);
                Rect card = CardRect(project);
                bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, card.xMin), Mathf.Min(bounds.yMin, card.yMin),
                    Mathf.Max(bounds.xMax, card.xMax), Mathf.Max(bounds.yMax, card.yMax)) : card;
                any = true;
            }
            tabBounds = any ? bounds.ExpandedBy(CanvasMargin) : new Rect(0f, 0f, 400f, 300f);
        }

        private void RefreshChain()
        {
            var selected = Selected;
            if (chainFor == selected && chainTab == CurTab) return;
            chainFor = selected;
            chainTab = CurTab;
            chain.Clear();
            if (selected == null || !ResearchOrganizedMod.settings.highlightChain) return;
            var stack = new Stack<ResearchProjectDef>();
            stack.Push(selected);
            while (stack.Count > 0)
            {
                var project = stack.Pop();
                if (!chain.Add(project) || project.prerequisites == null) continue;
                foreach (var prerequisite in project.prerequisites)
                    if (prerequisite != null && tabProjectSet.Contains(prerequisite)) stack.Push(prerequisite);
            }
        }

        private void DrawCanvas(Rect rect)
        {
            lastCanvas = rect;
            if (Event.current.type == EventType.Repaint)
            {
                Rounded.BeginBatch();
                try
                {
                    Rounded.Fill(rect, Theme.BGDark);
                    Rounded.Border(rect, Color.Lerp(Theme.BGDark, Theme.HighlightGray, 0.35f));
                }
                finally
                {
                    Rounded.EndBatch();
                }
            }
            if (CurTab == null) return;

            if (!Find.ResearchManager.TabInfoVisible(CurTab))
            {
                notDiscovered = notDiscovered ?? "ResearchNotDiscovered".Translate().ToString();
                CoreWidgets.Line(rect, notDiscovered, Theme.TextFaint, GameFont.Small, TextAnchor.MiddleCenter);
                return;
            }

            RefreshTabProjects();
            var view = View(CurTab);
            Rect local = rect.AtZero().ContractedBy(1f);
            view.content = tabBounds;

            // A project selected from outside (an alert, an info card link) is brought into view.
            if (Selected != lastSelected)
            {
                if (!selectingHere && Selected != null && Selected.tab == CurTab) centerPending = Selected;
                lastSelected = Selected;
            }
            selectingHere = false;
            RefreshChain();

            if (fitPending)
            {
                fitPending = false;
                view.Fit(local, tabBounds, 0f, 1f);
            }
            else if (homedTabs.Add(CurTab))
            {
                // First look at this tab: full size, top-left corner of the projects in view.
                view.Home(new Vector2(local.x, local.y) - tabBounds.position, 1f);
            }
            if (centerPending != null && centerPending.tab == CurTab && tabProjectSet.Contains(centerPending))
            {
                view.CentreOn(local, CardRect(centerPending).center);
                centerPending = null;
            }

            Widgets.BeginGroup(rect);
            try
            {
                HandleCardClick(view, local);
                view.HandleInput(local);
                if (Event.current.type == EventType.Repaint) DrawLines(view, local);
                DrawCards(view, local);
            }
            finally
            {
                Widgets.EndGroup();
            }
        }

        private void HandleCardClick(ZoomView view, Rect local)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !Mouse.IsOver(local)) return;
            for (int i = tabProjects.Count - 1; i >= 0; i--)
            {
                var project = tabProjects[i];
                if (!view.ToScreen(CardRect(project)).Contains(current.mousePosition)) continue;
                if (!project.IsHidden && Selected != project)
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    Selected = project;
                    selectingHere = true;
                    OnSelectionChanged();
                }
                current.Use();
                return;
            }
        }

        private void DrawLines(ZoomView view, Rect local)
        {
            var settings = ResearchOrganizedMod.settings;
            Color normal = Color.Lerp(Theme.BGLight, Theme.TextFaint, 0.35f);
            Color lit = Theme.HighlightBlue;
            var selected = Selected;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var project in tabProjects)
                {
                    var prerequisites = project.prerequisites;
                    if (prerequisites == null) continue;
                    for (int j = 0; j < prerequisites.Count; j++)
                    {
                        var prerequisite = prerequisites[j];
                        if (prerequisite == null || !tabProjectSet.Contains(prerequisite)) continue;
                        if (ResearchOrganizedMain.PrerequisiteLineTab(CurTab, this, project, prerequisite) == null) continue;
                        bool highlighted = selected == project || selected == prerequisite || (chain.Contains(project) && chain.Contains(prerequisite));
                        if (highlighted != (pass == 1)) continue;
                        Vector2 from = view.ToScreen(new Vector2(PosX(prerequisite) + CardWidth + ExtraWidth(prerequisite), PosY(prerequisite) + CardHeight / 2f));
                        Vector2 to = view.ToScreen(new Vector2(PosX(project), PosY(project) + CardHeight / 2f));
                        if (!LineInView(from, to, local)) continue;
                        DrawConnection(from, to, highlighted ? lit : normal, highlighted ? 3f : 2f, settings.curvedLines);
                    }
                }
            }
        }

        private static bool LineInView(Vector2 a, Vector2 b, Rect view) =>
            Mathf.Max(a.x, b.x) >= view.xMin && Mathf.Min(a.x, b.x) <= view.xMax && Mathf.Max(a.y, b.y) >= view.yMin && Mathf.Min(a.y, b.y) <= view.yMax;

        /// <summary>A straight line, or a horizontal S-curve drawn as short segments.</summary>
        private static void DrawConnection(Vector2 from, Vector2 to, Color color, float width, bool curved)
        {
            if (!curved || Mathf.Abs(from.y - to.y) < 1f)
            {
                Widgets.DrawLine(from, to, color, width);
                return;
            }
            float pull = Mathf.Max(20f, Mathf.Abs(to.x - from.x) * 0.5f);
            Vector2 c1 = new Vector2(from.x + pull, from.y);
            Vector2 c2 = new Vector2(to.x - pull, to.y);
            Vector2 previous = from;
            for (int i = 1; i <= CurveSegments; i++)
            {
                float t = i / (float)CurveSegments;
                float u = 1f - t;
                Vector2 point = u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to;
                Widgets.DrawLine(previous, point, color, width);
                previous = point;
            }
        }
    }
}
