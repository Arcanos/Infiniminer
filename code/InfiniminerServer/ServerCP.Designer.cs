namespace Infiniminer
{
    partial class ServerCP
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ServerCP));
            this.OptionViewTabs = new System.Windows.Forms.TabControl();
            this.StatusTab = new System.Windows.Forms.TabPage();
            this.ConsoleTab = new System.Windows.Forms.TabPage();
            this.UpdateTimer = new System.Windows.Forms.Timer(this.components);
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.OptionViewTabs.SuspendLayout();
            this.SuspendLayout();
            // 
            // OptionViewTabs
            // 
            this.OptionViewTabs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.OptionViewTabs.Controls.Add(this.StatusTab);
            this.OptionViewTabs.Controls.Add(this.ConsoleTab);
            this.OptionViewTabs.Location = new System.Drawing.Point(0, 0);
            this.OptionViewTabs.Name = "OptionViewTabs";
            this.OptionViewTabs.SelectedIndex = 0;
            this.OptionViewTabs.Size = new System.Drawing.Size(301, 264);
            this.OptionViewTabs.TabIndex = 1;
            // 
            // StatusTab
            // 
            this.StatusTab.Location = new System.Drawing.Point(4, 22);
            this.StatusTab.Name = "StatusTab";
            this.StatusTab.Padding = new System.Windows.Forms.Padding(3);
            this.StatusTab.Size = new System.Drawing.Size(293, 238);
            this.StatusTab.TabIndex = 0;
            this.StatusTab.Text = "Status";
            this.StatusTab.UseVisualStyleBackColor = true;
            // 
            // ConsoleTab
            // 
            this.ConsoleTab.Location = new System.Drawing.Point(4, 22);
            this.ConsoleTab.Name = "ConsoleTab";
            this.ConsoleTab.Padding = new System.Windows.Forms.Padding(3);
            this.ConsoleTab.Size = new System.Drawing.Size(316, 238);
            this.ConsoleTab.TabIndex = 1;
            this.ConsoleTab.Text = "Console";
            this.ConsoleTab.UseVisualStyleBackColor = true;
            // 
            // UpdateTimer
            // 
            this.UpdateTimer.Enabled = true;
            this.UpdateTimer.Interval = 400;
            // 
            // NotifyIcon
            // 
            this.NotifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("NotifyIcon.Icon")));
            this.NotifyIcon.Text = "Infiniminer Server";
            this.NotifyIcon.Visible = true;
            // 
            // ServerCP
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 264);
            this.Controls.Add(this.OptionViewTabs);
            this.Name = "ServerCP";
            this.Text = "ServerCP";
            this.OptionViewTabs.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl OptionViewTabs;
        private System.Windows.Forms.TabPage StatusTab;
        private System.Windows.Forms.TabPage ConsoleTab;
        private System.Windows.Forms.Timer UpdateTimer;
        private System.Windows.Forms.NotifyIcon NotifyIcon;
    }
}