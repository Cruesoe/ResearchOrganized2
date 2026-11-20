using System.Collections.Generic;
using System.Text;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace ResearchOrganized2
{
    public partial class MainTabWindow_Organized
    {
        private const int MaxCardIcons = 3;

        /// <summary>A project's unchanging card text and hover card, built once per project for the session's language.</summary>
        private sealed class CardInfo
        {
            public string label;
            public string cost;
            public string hoverBody;
            public readonly List<Def> icons = new List<Def>();
        }

        private static readonly Dictionary<ResearchProjectDef, CardInfo> cardInfo = new Dictionary<ResearchProjectDef, CardInfo>();
        private static LoadedLanguage cardLanguage;
        private static readonly Dictionary<int, string> fractions = new Dictionary<int, string>();
        private static readonly string[] percents = new string[101];

        private static Texture2D techprintIcon;
        private static Texture2D studyIcon;
        private static Texture2D doneIcon;
        private static Texture2D lockedIcon;

        private static CardInfo Info(ResearchProjectDef project)
        {
            if (cardLanguage != LanguageDatabase.activeLanguage)
            {
                cardInfo.Clear();
                cardLanguage = LanguageDatabase.activeLanguage;
            }
            if (cardInfo.TryGetValue(project, out var info)) return info;
            info = new CardInfo
            {
                label = project.LabelCap,
                cost = project.CostApparent.ToString("F0")
            };
            var unlocked = project.UnlockedDefs;
            for (int i = 0; i < unlocked.Count && info.icons.Count < MaxCardIcons; i++)
            {
                if (unlocked[i] is BuildableDef || unlocked[i] is RecipeDef recipe && recipe.ProducedThingDef != null)
                    info.icons.Add(unlocked[i]);
            }
            info.hoverBody = HoverBody(project);
            cardInfo[project] = info;
            return info;
        }

        private static string HoverBody(ResearchProjectDef project)
        {
            var text = new StringBuilder();
            if (!project.description.NullOrEmpty()) text.AppendLine(project.description).AppendLine();
            text.AppendLine("ResearchOrganized2_HoverCost".Translate(project.CostApparent.ToString("F0")));
            if (!project.prerequisites.NullOrEmpty())
            {
                text.Append("ResearchOrganized2_HoverNeeds".Translate()).Append(' ');
                for (int i = 0; i < project.prerequisites.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(project.prerequisites[i].LabelCap);
                }
                text.AppendLine();
            }
            var unlocked = project.UnlockedDefs;
            if (unlocked.Count > 0)
            {
                text.Append("ResearchOrganized2_HoverUnlocks".Translate()).Append(' ');
                for (int i = 0; i < unlocked.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(unlocked[i].LabelCap);
                }
            }
            return text.ToString().TrimEnd();
        }

        /// <summary>"a/b", cached by its two numbers.</summary>
        private static string Fraction(int done, int total)
        {
            int key = done * 10000 + total;
            if (!fractions.TryGetValue(key, out string text)) fractions[key] = text = done + "/" + total;
            return text;
        }

        private static string Percent(float fraction)
        {
            int percent = Mathf.Clamp(Mathf.FloorToInt(fraction * 100f), 0, 100);
            return percents[percent] ?? (percents[percent] = percent + "%");
        }

        private void DrawCards(ZoomView view, Rect local)
        {
            if (techprintIcon == null)
            {
                techprintIcon = CoreWidgets.Icon("scroll");
                studyIcon = CoreWidgets.Icon("magnifying-glass");
                doneIcon = CoreWidgets.Icon("check-circle");
                lockedIcon = CoreWidgets.Icon("lock");
            }
            var settings = ResearchOrganizedMod.settings;
            foreach (var project in tabProjects)
            {
                Rect card = view.ToScreen(CardRect(project));
                if (!card.Overlaps(local)) continue;
                DrawCard(project, card, view.zoom, settings);
            }
        }

        private void DrawCard(ResearchProjectDef project, Rect card, float zoom, ResearchOrganizedSettings settings)
        {
            var info = Info(project);
            bool current = Find.ResearchManager.IsCurrentProject(project);
            bool finished = project.IsFinished;
            bool locked = !finished && !current && !project.CanStartNow;
            bool selected = Selected == project;
            bool hidden = project.IsHidden;
            bool searchedOut = searchMatches != null && !searchMatches.Contains(project);
            float alpha = searchedOut ? 0.35f : 1f;
            Color era = settings.disableCustomColors ? Theme.HighlightGray : ResearchOrganizedMain.EraColor(project);
            float hover = searchedOut ? 0f : ThemeMotion.Hover(card, project.shortHash, Mouse.IsOver(card));

            if (Event.current.type == EventType.Repaint)
            {
                Color fill = finished ? Color.Lerp(Theme.BG, era, 0.5f)
                    : locked ? Color.Lerp(Theme.BGDark, era, 0.15f)
                    : Color.Lerp(Theme.BG, era, 0.28f);
                Color edge = ResearchOrganizedLayout.cyclicNodes.Contains(project) ? Theme.HighlightRed
                    : selected ? Color.white
                    : current ? Theme.HighlightYellow
                    : chain.Contains(project) ? Theme.HighlightBlue
                    : locked ? Theme.BGLight
                    : era;
                Rounded.BeginBatch();
                try
                {
                    Rounded.Fill(card, Faces.Fade(fill, alpha));
                    if (hover > 0f) Rounded.Fill(card.ContractedBy(2f), new Color(1f, 1f, 1f, 0.06f * hover));
                    Rounded.Border(card, Faces.Fade(edge, alpha));
                    if (selected || current) Rounded.Border(card.ContractedBy(1f), Faces.Fade(edge, 0.8f * alpha));
                }
                finally
                {
                    Rounded.EndBatch();
                }
                if (current || hover > 0f) CoreWidgets.EdgeGlow(card, Faces.Fade(current ? Theme.HighlightYellow : era, alpha));
                if (searchMatches != null && !searchedOut) CoreWidgets.EdgeGlow(card, Theme.HighlightYellow);

                float progress = project.ProgressPercent;
                if (settings.cardProgressStrip && progress > 0f && !finished)
                {
                    Rect strip = new Rect(card.x + 3f, card.yMax - 4f, (card.width - 6f) * Mathf.Clamp01(progress), 2f);
                    Theme.Fill(strip, Faces.Fade(current ? Theme.HighlightYellow : Theme.HighlightBlue, alpha));
                }
                if (ResearchOrganizedLayout.IsFoundationTech(project))
                {
                    GUI.color = Faces.Fade(Theme.HighlightYellow, 0.9f * alpha);
                    GUI.DrawTexture(new Rect(card.xMax - 12f * zoom, card.y, 12f * zoom, 12f * zoom), ResearchOrganizedMain.FoundationNotchTex);
                    GUI.color = Color.white;
                }
                float extra = ExtraWidth(project) * zoom;
                if (extra > 0f)
                {
                    GUI.color = Faces.Fade(project.knowledgeCategory.color, alpha);
                    GUI.DrawTexture(new Rect(card.x + 3f, card.y, extra, card.height), project.knowledgeCategory.Tex, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }

            if (zoom >= 0.6f) DrawCardText(project, info, card, zoom, finished, current, locked, hidden, alpha, settings);

            if (settings.cardHoverCard && !hidden && Mouse.IsOver(card))
                CoreWidgets.Tip(card, info.label, info.hoverBody, null, era);
        }

        private void DrawCardText(ResearchProjectDef project, CardInfo info, Rect card, float zoom, bool finished, bool current, bool locked,
            bool hidden, float alpha, ResearchOrganizedSettings settings)
        {
            float left = card.x + 6f + ExtraWidth(project) * zoom;
            float width = card.xMax - 6f - left;
            Color text = Faces.Fade(locked ? Theme.TextDim : Color.white, alpha);
            string label = hidden ? TranslationCache.Get("ResearchOrganized2_UnknownProject") : info.label;
            if (!M.twoRows)
            {
                // Compact cards hold the name only, centered.
                CoreWidgets.Line(new Rect(left, card.y, width, card.height), label, text);
                return;
            }
            CoreWidgets.Line(new Rect(left, card.y + 2f, width, 22f), label, text);
            if (zoom < 0.85f || hidden) return;

            // Second row: status on the left, requirement chips and unlock icons on the right.
            float iconSize = M.iconSize;
            Rect row = new Rect(left, card.yMax - Mathf.Max(24f, iconSize + 6f), width, Mathf.Max(20f, iconSize + 4f));
            float right = row.xMax;
            if (settings.cardUnlockIcons)
            {
                for (int i = info.icons.Count - 1; i >= 0 && right - iconSize > row.x + 50f; i--)
                {
                    Rect icon = new Rect(right - iconSize, row.y + 2f, iconSize, iconSize);
                    Widgets.DefIcon(icon, info.icons[i]);
                    right = icon.x - 3f;
                }
            }
            if (settings.cardChips)
            {
                if (project.TechprintCount > 0)
                    right = Counter(row, right, techprintIcon, Fraction(project.TechprintsApplied, project.TechprintCount), project.TechprintRequirementMet, alpha);
                if (project.RequiredAnalyzedThingCount > 0)
                    right = Counter(row, right, studyIcon, Fraction(project.AnalyzedThingsCompleted, project.RequiredAnalyzedThingCount), project.AnalyzedThingsRequirementsMet, alpha);
            }

            Rect status = new Rect(row.x, row.y, Mathf.Max(0f, right - row.x - 2f), row.height);
            if (finished)
            {
                CoreWidgets.DrawIcon(new Rect(status.x, status.y + 3f, 14f, 14f), doneIcon, Faces.Fade(Theme.HighlightGreen, alpha));
            }
            else if (current)
            {
                CoreWidgets.Line(status, Percent(project.ProgressPercent), Faces.Fade(Theme.TextYellow, alpha));
            }
            else
            {
                if (locked)
                {
                    CoreWidgets.DrawIcon(new Rect(status.x, status.y + 3f, 14f, 14f), lockedIcon, Faces.Fade(Theme.TextFaint, alpha));
                    status.xMin += 18f;
                }
                CoreWidgets.Line(status, info.cost, Faces.Fade(locked ? Theme.TextFaint : Theme.TextDim, alpha));
            }
        }

        /// <summary>An icon and "a/b" right-aligned before <paramref name="right"/>; returns the new right edge.</summary>
        private static float Counter(Rect row, float right, Texture icon, string text, bool met, float alpha)
        {
            Text.Font = GameFont.Small;
            float textWidth = Text.CalcSize(text).x;
            Rect label = new Rect(right - textWidth, row.y, textWidth, row.height);
            Rect iconRect = new Rect(label.x - 16f, row.y + 3f, 14f, 14f);
            if (iconRect.x < row.x + 40f) return right;
            Color color = Faces.Fade(met ? Theme.TextDim : Theme.TextRed, alpha);
            CoreWidgets.DrawIcon(iconRect, icon, color);
            CoreWidgets.Line(label, text, color);
            return iconRect.x - 5f;
        }
    }
}
