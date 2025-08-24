namespace Pulsar.Shared.Splash
{
    partial class SplashScreen
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SplashScreen));
            this.layoutTable = new System.Windows.Forms.TableLayoutPanel();
            this.progressText = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.Label();
            this.pulsarText = new System.Windows.Forms.PictureBox();
            this.throbber = new System.Windows.Forms.PictureBox();
            this.layoutTable.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pulsarText)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.throbber)).BeginInit();
            this.SuspendLayout();
            // 
            // layoutTable
            // 
            this.layoutTable.BackColor = System.Drawing.Color.Transparent;
            this.layoutTable.ColumnCount = 2;
            this.layoutTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.layoutTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.layoutTable.Controls.Add(this.progressText, 0, 1);
            this.layoutTable.Controls.Add(this.progressBar, 0, 2);
            this.layoutTable.Controls.Add(this.pulsarText, 1, 0);
            this.layoutTable.Controls.Add(this.throbber, 0, 0);
            this.layoutTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutTable.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
            this.layoutTable.Location = new System.Drawing.Point(10, 10);
            this.layoutTable.Margin = new System.Windows.Forms.Padding(0);
            this.layoutTable.Name = "layoutTable";
            this.layoutTable.RowCount = 3;
            this.layoutTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 80F));
            this.layoutTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.layoutTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.layoutTable.Size = new System.Drawing.Size(655, 355);
            this.layoutTable.TabIndex = 0;
            // 
            // progressText
            // 
            this.layoutTable.SetColumnSpan(this.progressText, 2);
            this.progressText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressText.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.progressText.Location = new System.Drawing.Point(0, 284);
            this.progressText.Margin = new System.Windows.Forms.Padding(0);
            this.progressText.Name = "progressText";
            this.progressText.Padding = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.progressText.Size = new System.Drawing.Size(655, 35);
            this.progressText.TabIndex = 0;
            this.progressText.Text = "[Progress Information]";
            this.progressText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            this.progressBar.BackColor = System.Drawing.Color.DimGray;
            this.layoutTable.SetColumnSpan(this.progressBar, 2);
            this.progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressBar.Location = new System.Drawing.Point(0, 319);
            this.progressBar.Margin = new System.Windows.Forms.Padding(0);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(655, 36);
            this.progressBar.TabIndex = 2;
            // 
            // pulsarText
            // 
            this.pulsarText.BackColor = System.Drawing.Color.Transparent;
            this.pulsarText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pulsarText.Image = ((System.Drawing.Image)(resources.GetObject("pulsarText.Image")));
            this.pulsarText.Location = new System.Drawing.Point(294, 0);
            this.pulsarText.Margin = new System.Windows.Forms.Padding(0);
            this.pulsarText.Name = "pulsarText";
            this.pulsarText.Size = new System.Drawing.Size(361, 284);
            this.pulsarText.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pulsarText.TabIndex = 3;
            this.pulsarText.TabStop = false;
            // 
            // throbber
            // 
            this.throbber.BackColor = System.Drawing.Color.Transparent;
            this.throbber.Dock = System.Windows.Forms.DockStyle.Fill;
            this.throbber.Image = ((System.Drawing.Image)(resources.GetObject("throbber.Image")));
            this.throbber.Location = new System.Drawing.Point(0, 0);
            this.throbber.Margin = new System.Windows.Forms.Padding(0);
            this.throbber.Name = "throbber";
            this.throbber.Size = new System.Drawing.Size(294, 284);
            this.throbber.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.throbber.TabIndex = 1;
            this.throbber.TabStop = false;
            // 
            // SplashScreen
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(675, 375);
            this.Controls.Add(this.layoutTable);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "SplashScreen";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Pulsar";
            this.layoutTable.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pulsarText)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.throbber)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layoutTable;
        private System.Windows.Forms.PictureBox throbber;
        private System.Windows.Forms.Label progressBar;
        private System.Windows.Forms.Label progressText;
        private System.Windows.Forms.PictureBox pulsarText;
    }
}
