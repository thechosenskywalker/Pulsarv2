namespace Pulsar.Server.Forms
{
    partial class FrmTaskCommand
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmTaskCommand));
            ShellComboBox = new System.Windows.Forms.ComboBox();
            label1 = new System.Windows.Forms.Label();
            CTextBox = new System.Windows.Forms.TextBox();
            label2 = new System.Windows.Forms.Label();
            button2 = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // ShellComboBox
            // 
            ShellComboBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ShellComboBox.FormattingEnabled = true;
            ShellComboBox.Items.AddRange(new object[] { "Command Prompt", "Powershell" });
            ShellComboBox.Location = new System.Drawing.Point(14, 29);
            ShellComboBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ShellComboBox.Name = "ShellComboBox";
            ShellComboBox.Size = new System.Drawing.Size(476, 23);
            ShellComboBox.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(14, 10);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(32, 15);
            label1.TabIndex = 1;
            label1.Text = "Shell";
            // 
            // CTextBox
            // 
            CTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            CTextBox.Location = new System.Drawing.Point(14, 78);
            CTextBox.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            CTextBox.Multiline = true;
            CTextBox.Name = "CTextBox";
            CTextBox.Size = new System.Drawing.Size(476, 123);
            CTextBox.TabIndex = 5;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(14, 57);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(64, 15);
            label2.TabIndex = 4;
            label2.Text = "Command";
            // 
            // button2
            // 
            button2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            button2.Location = new System.Drawing.Point(14, 207);
            button2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(477, 27);
            button2.TabIndex = 6;
            button2.Text = "Add Task";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // FrmTaskCommand
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(505, 248);
            Controls.Add(button2);
            Controls.Add(CTextBox);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(ShellComboBox);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "FrmTaskCommand";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Auto Task | Shell Command";
            Load += FrmTaskCommand_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox ShellComboBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox CTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button2;
    }
}