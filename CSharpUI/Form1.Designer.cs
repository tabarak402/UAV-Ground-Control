namespace UAVGroundControl
{
    partial class Form1
    {
        /// <summary>
        ///Gerekli tasarımcı değişkeni.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///Kullanılan tüm kaynakları temizleyin.
        /// </summary>
        ///<param name="disposing">yönetilen kaynaklar dispose edilmeliyse doğru; aksi halde yanlış.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer üretilen kod

        /// <summary>
        /// Tasarımcı desteği için gerekli metot - bu metodun 
        ///içeriğini kod düzenleyici ile değiştirmeyin.
        /// </summary>
        private void InitializeComponent()
        {
            this.speedGauge1 = new LiveCharts.WinForms.AngularGauge();
            this.altitudeGauge2 = new LiveCharts.WinForms.AngularGauge();
            this.headingGauge3 = new LiveCharts.WinForms.AngularGauge();
            this.powerGauge4 = new LiveCharts.WinForms.AngularGauge();
            this.SuspendLayout();
            // 
            // speedGauge1
            // 
            this.speedGauge1.Location = new System.Drawing.Point(1, 385);
            this.speedGauge1.Margin = new System.Windows.Forms.Padding(0);
            this.speedGauge1.Name = "speedGauge1";
            this.speedGauge1.Size = new System.Drawing.Size(149, 166);
            this.speedGauge1.TabIndex = 1;
            this.speedGauge1.Text = "angularGauge1";
            // 
            // altitudeGauge2
            // 
            this.altitudeGauge2.Location = new System.Drawing.Point(156, 385);
            this.altitudeGauge2.Name = "altitudeGauge2";
            this.altitudeGauge2.Size = new System.Drawing.Size(149, 166);
            this.altitudeGauge2.TabIndex = 2;
            this.altitudeGauge2.Text = "angularGauge1";
            // 
            // headingGauge3
            // 
            this.headingGauge3.Location = new System.Drawing.Point(311, 385);
            this.headingGauge3.Name = "headingGauge3";
            this.headingGauge3.Size = new System.Drawing.Size(149, 166);
            this.headingGauge3.TabIndex = 3;
            this.headingGauge3.Text = "angularGauge1";
            // 
            // powerGauge4
            // 
            this.powerGauge4.Location = new System.Drawing.Point(476, 385);
            this.powerGauge4.Name = "powerGauge4";
            this.powerGauge4.Size = new System.Drawing.Size(149, 166);
            this.powerGauge4.TabIndex = 4;
            this.powerGauge4.Text = "angularGauge1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(869, 510);
            this.Controls.Add(this.powerGauge4);
            this.Controls.Add(this.speedGauge1);
            this.Controls.Add(this.headingGauge3);
            this.Controls.Add(this.altitudeGauge2);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private LiveCharts.WinForms.AngularGauge speedGauge;
        private LiveCharts.WinForms.AngularGauge speedGauge1;
        private LiveCharts.WinForms.AngularGauge altitudeGauge2;
        private LiveCharts.WinForms.AngularGauge headingGauge3;
        private LiveCharts.WinForms.AngularGauge powerGauge4;
    }
}

