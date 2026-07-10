using System;
using System.Drawing;
using System.Windows.Forms;

namespace CustomRPC
{
    public partial class MainForm
    {
        const int LayoutDesignFormWidth = 480;
        const int LayoutContentLeft = 12;
        const int LayoutFieldLeftOffset = 47;
        const int LayoutSeparatorPadding = 8;
        const int LayoutFieldRowGap = 8;
        const int LayoutTimestampLeft = 15;
        const int LayoutActivitiesTextGap = 4;
        const int LayoutActivitiesSlotActionsGap = 8;
        const int LayoutActivitiesPanelBottomGap = 4;
        const int LayoutActivitiesSectionBottomGap = 4;
        const int LayoutBottomGap = 4;
        const int ActivitiesListWidth = 475;
        const int ActivitiesNameColumnWidth = 96;
        const int ActivitiesTypeColumnWidth = 71;
        const int ActivitiesApplicationIdColumnWidth = 145;
        const int ActivitiesStatusColumnWidth = 86;
        const int ActivitiesEnabledColumnWidth = 60;

        bool layoutInitialized;

        void InitializeMainFormLayout()
        {
            Resize += (_, __) => ApplyMainFormLayout();
            layoutInitialized = true;
            ApplyMainFormLayout();
        }

        void ApplyMainFormLayout()
        {
            if (!layoutInitialized || panelActivities == null)
                return;

            LayoutActivitiesSection();

            int centerDelta = (ClientSize.Width - LayoutDesignFormWidth) / 2;
            int blockLeft = LayoutContentLeft + centerDelta;
            int fieldLeft = blockLeft + LayoutFieldLeftOffset;

            int y = panelActivities.Bottom;
            y += LayoutActivitiesSectionBottomGap;
            panelSeparator1.Location = new Point(0, y);
            panelSeparator1.Height = 1;
            y += 1 + LayoutSeparatorPadding;

            PlaceIdTypeDisplayRow(blockLeft, y);
            y = Math.Max(comboBoxDisplay.Bottom, labelDisplay.Bottom);

            PlaceLabelFieldRow(labelName, textBoxName, blockLeft, fieldLeft, y + LayoutFieldRowGap);
            y = textBoxName.Bottom;

            PlaceDetailsRow(blockLeft, fieldLeft, y + LayoutFieldRowGap);
            y = textBoxDetailsURL.Bottom;

            PlaceStateRow(blockLeft, fieldLeft, y + LayoutFieldRowGap);
            y = textBoxStateURL.Bottom;

            PlaceLabelFieldRow(labelParty, flowLayoutPanelParty, blockLeft, fieldLeft, y + LayoutFieldRowGap);
            y = flowLayoutPanelParty.Bottom;

            PlaceSeparator(panelSeparator2, ref y);

            labelTimestamp.Location = new Point(LayoutTimestampLeft, y);
            panelTimestamps.Location = new Point(LayoutTimestampLeft, labelTimestamp.Bottom + 4);
            y = panelTimestamps.Bottom;

            PlaceSeparator(panelSeparator3, ref y);

            PlaceImageSection(blockLeft, y);
            y = Math.Max(textBoxLargeURL.Bottom, textBoxSmallURL.Bottom);

            PlaceSeparator(panelSeparator4, ref y);

            PlaceButtonSection(blockLeft, y);

            int contentBottom = Math.Max(textBoxButton1URL.Bottom, textBoxButton2URL.Bottom);
            PositionBottomButtonRow(contentBottom);

            foreach (var separator in new[] { panelSeparator1, panelSeparator2, panelSeparator3, panelSeparator4 })
            {
                separator.Width = ClientSize.Width;
                separator.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            if (tableLayoutPanelButtons != null)
                tableLayoutPanelButtons.MinimumSize = new Size(ClientSize.Width, 0);
        }

        void PositionBottomButtonRow(int contentBottom)
        {
            if (tableLayoutPanelButtons == null)
                return;

            tableLayoutPanelButtons.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanelButtons.Location = new Point(0, contentBottom + LayoutBottomGap);
            tableLayoutPanelButtons.Width = ClientSize.Width;

            int neededHeight = tableLayoutPanelButtons.Bottom + LayoutBottomGap;
            if (statusStrip != null)
                neededHeight += statusStrip.Height;
            ClientSize = new Size(ClientSize.Width, neededHeight);
            MinimumSize = new Size(MinimumSize.Width, neededHeight);
        }

        void SyncActivitiesListColumns()
        {
            if (listViewSlots == null || listViewSlots.Columns.Count < 5)
                return;

            listViewSlots.Columns[0].Width = ActivitiesNameColumnWidth;
            listViewSlots.Columns[1].Width = ActivitiesTypeColumnWidth;
            listViewSlots.Columns[2].Width = ActivitiesApplicationIdColumnWidth;
            listViewSlots.Columns[3].Width = ActivitiesStatusColumnWidth;
            listViewSlots.Columns[4].Width = IsMultiSlotRpcMode() ? ActivitiesEnabledColumnWidth : 0;
        }

        void ApplyActivitiesListColumnWidths()
        {
            SyncActivitiesListColumns();
        }

        void LayoutActivitiesSection()
        {
            int margin = 15;
            int contentWidth = Math.Min(ActivitiesListWidth, Math.Max(400, panelActivities.ClientSize.Width - margin * 2));
            int contentLeft = Math.Max(margin, (panelActivities.ClientSize.Width - contentWidth) / 2);

            int cy = 8;
            if (tableLayoutPanelActivitiesTitle != null)
            {
                tableLayoutPanelActivitiesTitle.Location = new Point(contentLeft, cy);
                cy = tableLayoutPanelActivitiesTitle.Bottom + LayoutSeparatorPadding;
            }

            listViewSlots.Location = new Point(contentLeft, cy);
            listViewSlots.Width = contentWidth;
            ApplyActivitiesListColumnWidths();
            flowLayoutPanelSlotActions.Location = new Point(contentLeft, listViewSlots.Bottom + LayoutActivitiesSlotActionsGap);

            int neededHeight = flowLayoutPanelSlotActions.Bottom + LayoutActivitiesPanelBottomGap;
            if (panelActivities.Height != neededHeight)
                panelActivities.Height = neededHeight;
        }

        static int MeasureWrappedLabelHeight(Label label, int width)
        {
            if (string.IsNullOrEmpty(label.Text))
                return 0;

            return TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak).Height;
        }

        /// <summary>
        /// Places a separator with equal padding above and below the line.
        /// Caller sets y to the bottom of the content above; padding-before is applied here.
        /// After return, y is ready for the next content below.
        /// </summary>
        static void PlaceSeparator(Panel separator, ref int y)
        {
            y += LayoutSeparatorPadding;
            separator.Location = new Point(0, y);
            separator.Height = 1;
            y += 1 + LayoutSeparatorPadding;
        }

        void PlaceIdTypeDisplayRow(int blockLeft, int y)
        {
            labelID.Location = new Point(blockLeft, y + 3);
            textBoxID.Location = new Point(blockLeft + 24, y);
            labelType.Location = new Point(blockLeft + 169, y + 3);
            comboBoxType.Location = new Point(blockLeft + 209, y);
            labelDisplay.Location = new Point(blockLeft + 345, y + 3);
            comboBoxDisplay.Location = new Point(blockLeft + 393, y);
        }

        static void PlaceLabelFieldRow(Control label, Control field, int blockLeft, int fieldLeft, int y)
        {
            label.Location = new Point(blockLeft, y + 3);
            field.Location = new Point(fieldLeft, y);
        }

        void PlaceDetailsRow(int blockLeft, int fieldLeft, int y)
        {
            labelDetails.Location = new Point(blockLeft, y + 3);
            textBoxDetails.Location = new Point(fieldLeft, y);
            labelDetailsURL.Location = new Point(blockLeft + 270, y + 3);
            textBoxDetailsURL.Location = new Point(blockLeft + 303, y);
        }

        void PlaceStateRow(int blockLeft, int fieldLeft, int y)
        {
            labelState.Location = new Point(blockLeft, y + 3);
            textBoxState.Location = new Point(fieldLeft, y);
            labelStateURL.Location = new Point(blockLeft + 270, y + 3);
            textBoxStateURL.Location = new Point(blockLeft + 303, y);
        }

        void PlaceImageSection(int blockLeft, int y)
        {
            labelLarge.Location = new Point(blockLeft + 35, y);
            labelSmall.Location = new Point(blockLeft + 264, y);

            comboBoxLargeKey.Location = new Point(blockLeft + 38, y + 18);
            comboBoxSmallKey.Location = new Point(blockLeft + 267, y + 18);

            labelLargeKey.Location = new Point(blockLeft, y + 21);
            labelSmallKey.Location = new Point(blockLeft + 229, y + 21);

            labelLargeText.Location = new Point(blockLeft, y + 51);
            labelSmallText.Location = new Point(blockLeft + 229, y + 51);

            textBoxLargeText.Location = new Point(blockLeft + 38, y + 48);
            textBoxSmallText.Location = new Point(blockLeft + 267, y + 48);

            labelLargeURL.Location = new Point(blockLeft, y + 81);
            labelSmallURL.Location = new Point(blockLeft + 229, y + 81);

            textBoxLargeURL.Location = new Point(blockLeft + 38, y + 78);
            textBoxSmallURL.Location = new Point(blockLeft + 267, y + 78);
        }

        void PlaceButtonSection(int blockLeft, int y)
        {
            labelButton1.Location = new Point(blockLeft + 35, y);
            labelButton2.Location = new Point(blockLeft + 264, y);

            labelButton1Text.Location = new Point(blockLeft, y + 18);
            labelButton2Text.Location = new Point(blockLeft + 229, y + 18);
            textBoxButton1Text.Location = new Point(blockLeft + 38, y + 18);
            textBoxButton2Text.Location = new Point(blockLeft + 267, y + 18);

            labelButton1URL.Location = new Point(blockLeft, y + 48);
            labelButton2URL.Location = new Point(blockLeft + 229, y + 48);
            textBoxButton1URL.Location = new Point(blockLeft + 38, y + 45);
            textBoxButton2URL.Location = new Point(blockLeft + 267, y + 45);
        }

    }
}
