namespace Pulsar.Server.Forms
{
    partial class FrmRemoteShell
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmRemoteShell));
            txtConsoleOutput = new System.Windows.Forms.RichTextBox();
            txtConsoleInput = new System.Windows.Forms.TextBox();
            tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
            togglePowerShellToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            copyAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            tableLayoutPanel.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // txtConsoleOutput
            // 
            txtConsoleOutput.BackColor = System.Drawing.Color.Black;
            txtConsoleOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtConsoleOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            txtConsoleOutput.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            txtConsoleOutput.ForeColor = System.Drawing.Color.WhiteSmoke;
            txtConsoleOutput.Location = new System.Drawing.Point(3, 3);
            txtConsoleOutput.Name = "txtConsoleOutput";
            txtConsoleOutput.ReadOnly = true;
            txtConsoleOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            txtConsoleOutput.Size = new System.Drawing.Size(734, 373);
            txtConsoleOutput.TabIndex = 1;
            txtConsoleOutput.Text = "";
            txtConsoleOutput.TextChanged += txtConsoleOutput_TextChanged;
            txtConsoleOutput.KeyPress += txtConsoleOutput_KeyPress;
            // 
            // txtConsoleInput
            // 
            txtConsoleInput.BackColor = System.Drawing.Color.Black;
            txtConsoleInput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtConsoleInput.Dock = System.Windows.Forms.DockStyle.Fill;
            txtConsoleInput.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            txtConsoleInput.ForeColor = System.Drawing.Color.WhiteSmoke;
            txtConsoleInput.Location = new System.Drawing.Point(3, 382);
            txtConsoleInput.MaxLength = 0;
            txtConsoleInput.Multiline = true;
            txtConsoleInput.Name = "txtConsoleInput";
            txtConsoleInput.Size = new System.Drawing.Size(734, 57);
            txtConsoleInput.TabIndex = 0;
            txtConsoleInput.TextChanged += txtConsoleInput_TextChanged;
            txtConsoleInput.KeyDown += txtConsoleInput_KeyDown;
            // 
            // tableLayoutPanel
            // 
            tableLayoutPanel.BackColor = System.Drawing.Color.Black;
            tableLayoutPanel.ColumnCount = 1;
            tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel.Controls.Add(txtConsoleOutput, 0, 0);
            tableLayoutPanel.Controls.Add(txtConsoleInput, 0, 1);
            tableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel.Name = "tableLayoutPanel";
            tableLayoutPanel.RowCount = 2;
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 63F));
            tableLayoutPanel.Size = new System.Drawing.Size(740, 442);
            tableLayoutPanel.TabIndex = 2;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { togglePowerShellToolStripMenuItem, copyToolStripMenuItem, copyAllToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(185, 70);
            // 
            // togglePowerShellToolStripMenuItem
            // 
            togglePowerShellToolStripMenuItem.Image = Properties.Resources.application_osx_terminal;
            togglePowerShellToolStripMenuItem.Name = "togglePowerShellToolStripMenuItem";
            togglePowerShellToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            togglePowerShellToolStripMenuItem.Text = "Switch to PowerShell";
            togglePowerShellToolStripMenuItem.Click += togglePowerShellToolStripMenuItem_Click;
            // 
            // copyToolStripMenuItem
            // 
            copyToolStripMenuItem.Image = Properties.Resources.clipboard_paste_image;
            copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            copyToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            copyToolStripMenuItem.Text = "Copy Selected";
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;
            // 
            // copyAllToolStripMenuItem
            // 
            copyAllToolStripMenuItem.Image = Properties.Resources.selectall;
            copyAllToolStripMenuItem.Name = "copyAllToolStripMenuItem";
            copyAllToolStripMenuItem.Size = new System.Drawing.Size(184, 22);
            copyAllToolStripMenuItem.Text = "Copy All";
            copyAllToolStripMenuItem.Click += copyAllToolStripMenuItem_Click;
            // 
            // FrmRemoteShell
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(740, 442);
            Controls.Add(tableLayoutPanel);
            Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Name = "FrmRemoteShell";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Remote Shell []";
            FormClosing += FrmRemoteShell_FormClosing;
            Load += FrmRemoteShell_Load;
            tableLayoutPanel.ResumeLayout(false);
            tableLayoutPanel.PerformLayout();
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TextBox txtConsoleInput;
        private System.Windows.Forms.RichTextBox txtConsoleOutput;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem togglePowerShellToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyAllToolStripMenuItem;
    }
}