namespace Pulsar.Server.Forms
{
    partial class FrmVisitWebsite
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmVisitWebsite));
            chkVisitHidden = new System.Windows.Forms.CheckBox();
            lblURL = new System.Windows.Forms.Label();
            txtURL = new System.Windows.Forms.TextBox();
            btnVisitWebsite = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // chkVisitHidden
            // 
            chkVisitHidden.AutoSize = true;
            chkVisitHidden.Location = new System.Drawing.Point(48, 38);
            chkVisitHidden.Name = "chkVisitHidden";
            chkVisitHidden.Size = new System.Drawing.Size(170, 17);
            chkVisitHidden.TabIndex = 2;
            chkVisitHidden.Text = "Visit hidden (recommended)";
            chkVisitHidden.UseVisualStyleBackColor = true;
            // 
            // lblURL
            // 
            lblURL.AutoSize = true;
            lblURL.Location = new System.Drawing.Point(12, 9);
            lblURL.Name = "lblURL";
            lblURL.Size = new System.Drawing.Size(30, 13);
            lblURL.TabIndex = 0;
            lblURL.Text = "URL:";
            // 
            // txtURL
            // 
            txtURL.Location = new System.Drawing.Point(48, 6);
            txtURL.Name = "txtURL";
            txtURL.Size = new System.Drawing.Size(336, 22);
            txtURL.TabIndex = 1;
            // 
            // btnVisitWebsite
            // 
            btnVisitWebsite.Location = new System.Drawing.Point(246, 34);
            btnVisitWebsite.Name = "btnVisitWebsite";
            btnVisitWebsite.Size = new System.Drawing.Size(138, 23);
            btnVisitWebsite.TabIndex = 3;
            btnVisitWebsite.Text = "Visit Website";
            btnVisitWebsite.UseVisualStyleBackColor = true;
            btnVisitWebsite.Click += btnVisitWebsite_Click;
            // 
            // FrmVisitWebsite
            // 
            AcceptButton = btnVisitWebsite;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(396, 72);
            Controls.Add(chkVisitHidden);
            Controls.Add(lblURL);
            Controls.Add(txtURL);
            Controls.Add(btnVisitWebsite);
            Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "FrmVisitWebsite";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Visit Website []";
            Load += FrmVisitWebsite_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.CheckBox chkVisitHidden;
        private System.Windows.Forms.Label lblURL;
        private System.Windows.Forms.TextBox txtURL;
        private System.Windows.Forms.Button btnVisitWebsite;
    }
}