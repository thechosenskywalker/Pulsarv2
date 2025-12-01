using Pulsar.Server.Controls;

namespace Pulsar.Server.Forms
{
    partial class FrmPasswordRecovery
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmPasswordRecovery));
            contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(components);
            saveToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            saveSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            copyToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            copyAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            copySelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            uRLToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            usernameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            passwordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            clearToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            clearAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            clearSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            refreshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            groupBox1 = new System.Windows.Forms.GroupBox();
            lstPasswords = new AeroListView();
            hIdentification = new System.Windows.Forms.ColumnHeader();
            hURL = new System.Windows.Forms.ColumnHeader();
            hUser = new System.Windows.Forms.ColumnHeader();
            hPass = new System.Windows.Forms.ColumnHeader();
            groupBox2 = new System.Windows.Forms.GroupBox();
            lblInfo = new System.Windows.Forms.Label();
            txtFormat = new System.Windows.Forms.TextBox();
            contextMenuStrip.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // contextMenuStrip
            // 
            contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { saveToFileToolStripMenuItem, copyToClipboardToolStripMenuItem, toolStripSeparator1, clearToolStripMenuItem, refreshToolStripMenuItem });
            contextMenuStrip.Name = "menuMain";
            contextMenuStrip.Size = new System.Drawing.Size(181, 120);
            // 
            // saveToFileToolStripMenuItem
            // 
            saveToFileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { saveAllToolStripMenuItem, saveSelectedToolStripMenuItem });
            saveToFileToolStripMenuItem.Image = Properties.Resources.save;
            saveToFileToolStripMenuItem.Name = "saveToFileToolStripMenuItem";
            saveToFileToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            saveToFileToolStripMenuItem.Text = "Save to File";
            // 
            // saveAllToolStripMenuItem
            // 
            saveAllToolStripMenuItem.Name = "saveAllToolStripMenuItem";
            saveAllToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            saveAllToolStripMenuItem.Text = "All";
            saveAllToolStripMenuItem.Click += saveAllToolStripMenuItem_Click;
            // 
            // saveSelectedToolStripMenuItem
            // 
            saveSelectedToolStripMenuItem.Name = "saveSelectedToolStripMenuItem";
            saveSelectedToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            saveSelectedToolStripMenuItem.Text = "Selected";
            saveSelectedToolStripMenuItem.Click += saveSelectedToolStripMenuItem_Click;
            // 
            // copyToClipboardToolStripMenuItem
            // 
            copyToClipboardToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { uRLToolStripMenuItem, usernameToolStripMenuItem, passwordToolStripMenuItem, copySelectedToolStripMenuItem, copyAllToolStripMenuItem });
            copyToClipboardToolStripMenuItem.Image = Properties.Resources.page_copy;
            copyToClipboardToolStripMenuItem.Name = "copyToClipboardToolStripMenuItem";
            copyToClipboardToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            copyToClipboardToolStripMenuItem.Text = "Copy to Clipboard";
            // 
            // copyAllToolStripMenuItem
            // 
            copyAllToolStripMenuItem.Image = Properties.Resources.selectall;
            copyAllToolStripMenuItem.Name = "copyAllToolStripMenuItem";
            copyAllToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            copyAllToolStripMenuItem.Text = "All Rows";
            copyAllToolStripMenuItem.Click += copyAllToolStripMenuItem_Click;
            // 
            // copySelectedToolStripMenuItem
            // 
            copySelectedToolStripMenuItem.Image = Properties.Resources.add;
            copySelectedToolStripMenuItem.Name = "copySelectedToolStripMenuItem";
            copySelectedToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            copySelectedToolStripMenuItem.Text = "Selected Row";
            copySelectedToolStripMenuItem.Click += copySelectedToolStripMenuItem_Click;
            // 
            // uRLToolStripMenuItem
            // 
            uRLToolStripMenuItem.Image = Properties.Resources.application_edit;
            uRLToolStripMenuItem.Name = "uRLToolStripMenuItem";
            uRLToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            uRLToolStripMenuItem.Text = "URL";
            uRLToolStripMenuItem.Click += uRLToolStripMenuItem_Click;
            // 
            // usernameToolStripMenuItem
            // 
            usernameToolStripMenuItem.Image = Properties.Resources.information;
            usernameToolStripMenuItem.Name = "usernameToolStripMenuItem";
            usernameToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            usernameToolStripMenuItem.Text = "Username";
            usernameToolStripMenuItem.Click += usernameToolStripMenuItem_Click;
            // 
            // passwordToolStripMenuItem
            // 
            passwordToolStripMenuItem.Image = Properties.Resources.key_go;
            passwordToolStripMenuItem.Name = "passwordToolStripMenuItem";
            passwordToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            passwordToolStripMenuItem.Text = "Password";
            passwordToolStripMenuItem.Click += passwordToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // clearToolStripMenuItem
            // 
            clearToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { clearAllToolStripMenuItem, clearSelectedToolStripMenuItem });
            clearToolStripMenuItem.Image = Properties.Resources.delete;
            clearToolStripMenuItem.Name = "clearToolStripMenuItem";
            clearToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            clearToolStripMenuItem.Text = "Clear";
            // 
            // clearAllToolStripMenuItem
            // 
            clearAllToolStripMenuItem.Name = "clearAllToolStripMenuItem";
            clearAllToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            clearAllToolStripMenuItem.Text = "All";
            clearAllToolStripMenuItem.Click += clearAllToolStripMenuItem_Click;
            // 
            // clearSelectedToolStripMenuItem
            // 
            clearSelectedToolStripMenuItem.Name = "clearSelectedToolStripMenuItem";
            clearSelectedToolStripMenuItem.Size = new System.Drawing.Size(118, 22);
            clearSelectedToolStripMenuItem.Text = "Selected";
            clearSelectedToolStripMenuItem.Click += clearSelectedToolStripMenuItem_Click;
            // 
            // refreshToolStripMenuItem
            // 
            refreshToolStripMenuItem.Image = Properties.Resources.refresh;
            refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
            refreshToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            refreshToolStripMenuItem.Text = "Refresh";
            refreshToolStripMenuItem.Click += refreshToolStripMenuItem_Click;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBox1.Controls.Add(lstPasswords);
            groupBox1.Location = new System.Drawing.Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(549, 325);
            groupBox1.TabIndex = 1;
            groupBox1.TabStop = false;
            groupBox1.Text = "Recovered Accounts";
            // 
            // lstPasswords
            // 
            lstPasswords.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lstPasswords.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { hIdentification, hURL, hUser, hPass });
            lstPasswords.ContextMenuStrip = contextMenuStrip;
            lstPasswords.FullRowSelect = true;
            lstPasswords.Location = new System.Drawing.Point(6, 19);
            lstPasswords.Name = "lstPasswords";
            lstPasswords.Size = new System.Drawing.Size(537, 300);
            lstPasswords.TabIndex = 0;
            lstPasswords.UseCompatibleStateImageBehavior = false;
            lstPasswords.View = System.Windows.Forms.View.Details;
            // 
            // hIdentification
            // 
            hIdentification.Text = "Identification";
            hIdentification.Width = 107;
            // 
            // hURL
            // 
            hURL.Text = "URL / Location";
            hURL.Width = 151;
            // 
            // hUser
            // 
            hUser.Text = "Username";
            hUser.Width = 142;
            // 
            // hPass
            // 
            hPass.Text = "Password";
            hPass.Width = 133;
            // 
            // groupBox2
            // 
            groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBox2.Controls.Add(lblInfo);
            groupBox2.Controls.Add(txtFormat);
            groupBox2.Location = new System.Drawing.Point(12, 343);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(549, 90);
            groupBox2.TabIndex = 2;
            groupBox2.TabStop = false;
            groupBox2.Text = "Custom Saving/Copying Format";
            // 
            // lblInfo
            // 
            lblInfo.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblInfo.Location = new System.Drawing.Point(35, 50);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new System.Drawing.Size(467, 26);
            lblInfo.TabIndex = 1;
            lblInfo.Text = "You can change the way the accounts are saved by adjusting the format in the box above.\r\nAvailable variables: APP, URL, USER, PASS\r\n";
            lblInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtFormat
            // 
            txtFormat.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtFormat.Location = new System.Drawing.Point(6, 19);
            txtFormat.Name = "txtFormat";
            txtFormat.Size = new System.Drawing.Size(537, 22);
            txtFormat.TabIndex = 0;
            txtFormat.Text = "APP - URL - USER:PASS";
            txtFormat.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // FrmPasswordRecovery
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(573, 445);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MinimumSize = new System.Drawing.Size(589, 400);
            Name = "FrmPasswordRecovery";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Password Recovery []";
            FormClosing += FrmPasswordRecovery_FormClosing;
            Load += FrmPasswordRecovery_Load;
            contextMenuStrip.ResumeLayout(false);
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Controls.AeroListView lstPasswords;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ColumnHeader hIdentification;
        private System.Windows.Forms.ColumnHeader hURL;
        private System.Windows.Forms.ColumnHeader hUser;
        private System.Windows.Forms.ColumnHeader hPass;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem saveToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem clearToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refreshToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copySelectedToolStripMenuItem;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.TextBox txtFormat;
        private System.Windows.Forms.ToolStripMenuItem clearAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem uRLToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem usernameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem passwordToolStripMenuItem;
    }
}