namespace TriggerAnalyticsEventXML
{
	partial class TriggerAnalyticsEventForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TriggerAnalyticsEventForm));
            this.btnSendXML = new System.Windows.Forms.Button();
            this.btnInsertAnalyticsEventXML = new System.Windows.Forms.Button();
            this.pnlCommand = new System.Windows.Forms.Panel();
            this.btnValidateXML = new System.Windows.Forms.Button();
            this.chkIncludeOverlay = new System.Windows.Forms.CheckBox();
            this.txtResponse = new System.Windows.Forms.RichTextBox();
            this.lblResponse = new System.Windows.Forms.Label();
            this.lblDestinationPort = new System.Windows.Forms.Label();
            this.txtDestinationPort = new System.Windows.Forms.TextBox();
            this.lblDestinationAddress = new System.Windows.Forms.Label();
            this.txtDestinationAddress = new System.Windows.Forms.TextBox();
            this.pnlXml = new System.Windows.Forms.Panel();
            this.txtAnalyticsXML = new System.Windows.Forms.RichTextBox();
            this.pnlCommand.SuspendLayout();
            this.pnlXml.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSendXML
            // 
            this.btnSendXML.Location = new System.Drawing.Point(162, 72);
            this.btnSendXML.Name = "btnSendXML";
            this.btnSendXML.Size = new System.Drawing.Size(150, 23);
            this.btnSendXML.TabIndex = 0;
            this.btnSendXML.Text = "&Send XML";
            this.btnSendXML.UseVisualStyleBackColor = true;
            this.btnSendXML.Click += new System.EventHandler(this.btnSendXML_Click);
            // 
            // btnInsertAnalyticsEventXML
            // 
            this.btnInsertAnalyticsEventXML.Location = new System.Drawing.Point(6, 45);
            this.btnInsertAnalyticsEventXML.Name = "btnInsertAnalyticsEventXML";
            this.btnInsertAnalyticsEventXML.Size = new System.Drawing.Size(150, 23);
            this.btnInsertAnalyticsEventXML.TabIndex = 1;
            this.btnInsertAnalyticsEventXML.Text = "&Insert Analytics event XML";
            this.btnInsertAnalyticsEventXML.UseVisualStyleBackColor = true;
            this.btnInsertAnalyticsEventXML.Click += new System.EventHandler(this.btnInsertAnalyticsEventXML_Click);
            // 
            // pnlCommand
            // 
            this.pnlCommand.Controls.Add(this.btnValidateXML);
            this.pnlCommand.Controls.Add(this.chkIncludeOverlay);
            this.pnlCommand.Controls.Add(this.txtResponse);
            this.pnlCommand.Controls.Add(this.lblResponse);
            this.pnlCommand.Controls.Add(this.lblDestinationPort);
            this.pnlCommand.Controls.Add(this.txtDestinationPort);
            this.pnlCommand.Controls.Add(this.lblDestinationAddress);
            this.pnlCommand.Controls.Add(this.txtDestinationAddress);
            this.pnlCommand.Controls.Add(this.btnSendXML);
            this.pnlCommand.Controls.Add(this.btnInsertAnalyticsEventXML);
            this.pnlCommand.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlCommand.Location = new System.Drawing.Point(0, 351);
            this.pnlCommand.Name = "pnlCommand";
            this.pnlCommand.Size = new System.Drawing.Size(490, 217);
            this.pnlCommand.TabIndex = 2;
            // 
            // btnValidateXML
            // 
            this.btnValidateXML.Location = new System.Drawing.Point(6, 72);
            this.btnValidateXML.Name = "btnValidateXML";
            this.btnValidateXML.Size = new System.Drawing.Size(150, 23);
            this.btnValidateXML.TabIndex = 8;
            this.btnValidateXML.Text = "&Validate XML";
            this.btnValidateXML.UseVisualStyleBackColor = true;
            this.btnValidateXML.Click += new System.EventHandler(this.btnValidateXML_Click);
            // 
            // chkIncludeOverlay
            // 
            this.chkIncludeOverlay.AutoSize = true;
            this.chkIncludeOverlay.Location = new System.Drawing.Point(162, 49);
            this.chkIncludeOverlay.Name = "chkIncludeOverlay";
            this.chkIncludeOverlay.Size = new System.Drawing.Size(98, 17);
            this.chkIncludeOverlay.TabIndex = 7;
            this.chkIncludeOverlay.Text = "Include &overlay";
            this.chkIncludeOverlay.UseVisualStyleBackColor = true;
            // 
            // txtResponse
            // 
            this.txtResponse.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtResponse.Location = new System.Drawing.Point(0, 121);
            this.txtResponse.Name = "txtResponse";
            this.txtResponse.Size = new System.Drawing.Size(490, 96);
            this.txtResponse.TabIndex = 6;
            this.txtResponse.Text = "";
            // 
            // lblResponse
            // 
            this.lblResponse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblResponse.AutoSize = true;
            this.lblResponse.Location = new System.Drawing.Point(3, 105);
            this.lblResponse.Name = "lblResponse";
            this.lblResponse.Size = new System.Drawing.Size(79, 13);
            this.lblResponse.TabIndex = 1;
            this.lblResponse.Text = "Socket receive";
            // 
            // lblDestinationPort
            // 
            this.lblDestinationPort.AutoSize = true;
            this.lblDestinationPort.Location = new System.Drawing.Point(315, 0);
            this.lblDestinationPort.Name = "lblDestinationPort";
            this.lblDestinationPort.Size = new System.Drawing.Size(81, 13);
            this.lblDestinationPort.TabIndex = 5;
            this.lblDestinationPort.Text = "Destination port";
            // 
            // txtDestinationPort
            // 
            this.txtDestinationPort.Location = new System.Drawing.Point(318, 19);
            this.txtDestinationPort.Name = "txtDestinationPort";
            this.txtDestinationPort.Size = new System.Drawing.Size(100, 20);
            this.txtDestinationPort.TabIndex = 4;
            this.txtDestinationPort.Text = "9090";
            // 
            // lblDestinationAddress
            // 
            this.lblDestinationAddress.AutoSize = true;
            this.lblDestinationAddress.Location = new System.Drawing.Point(3, 3);
            this.lblDestinationAddress.Name = "lblDestinationAddress";
            this.lblDestinationAddress.Size = new System.Drawing.Size(100, 13);
            this.lblDestinationAddress.TabIndex = 3;
            this.lblDestinationAddress.Text = "Destination address";
            // 
            // txtDestinationAddress
            // 
            this.txtDestinationAddress.Location = new System.Drawing.Point(3, 19);
            this.txtDestinationAddress.Name = "txtDestinationAddress";
            this.txtDestinationAddress.Size = new System.Drawing.Size(299, 20);
            this.txtDestinationAddress.TabIndex = 2;
            this.txtDestinationAddress.Text = "localhost";
            // 
            // pnlXml
            // 
            this.pnlXml.Controls.Add(this.txtAnalyticsXML);
            this.pnlXml.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlXml.Location = new System.Drawing.Point(0, 0);
            this.pnlXml.Name = "pnlXml";
            this.pnlXml.Size = new System.Drawing.Size(490, 351);
            this.pnlXml.TabIndex = 3;
            // 
            // txtAnalyticsXML
            // 
            this.txtAnalyticsXML.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtAnalyticsXML.Location = new System.Drawing.Point(0, 0);
            this.txtAnalyticsXML.Name = "txtAnalyticsXML";
            this.txtAnalyticsXML.Size = new System.Drawing.Size(490, 351);
            this.txtAnalyticsXML.TabIndex = 0;
            this.txtAnalyticsXML.Text = "";
            this.txtAnalyticsXML.WordWrap = false;
            this.txtAnalyticsXML.TextChanged += new System.EventHandler(this.txtAnalyticsXML_TextChanged);
            // 
            // TriggerAnalyticsEventForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(490, 568);
            this.Controls.Add(this.pnlXml);
            this.Controls.Add(this.pnlCommand);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TriggerAnalyticsEventForm";
            this.Text = "Trigger Analytics Event";
            this.pnlCommand.ResumeLayout(false);
            this.pnlCommand.PerformLayout();
            this.pnlXml.ResumeLayout(false);
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button btnSendXML;
		private System.Windows.Forms.Button btnInsertAnalyticsEventXML;
		private System.Windows.Forms.Panel pnlCommand;
		private System.Windows.Forms.Panel pnlXml;
		private System.Windows.Forms.Label lblDestinationPort;
		private System.Windows.Forms.TextBox txtDestinationPort;
		private System.Windows.Forms.Label lblDestinationAddress;
		private System.Windows.Forms.TextBox txtDestinationAddress;
		private System.Windows.Forms.Label lblResponse;
		private System.Windows.Forms.RichTextBox txtAnalyticsXML;
		private System.Windows.Forms.RichTextBox txtResponse;
		private System.Windows.Forms.CheckBox chkIncludeOverlay;
        private System.Windows.Forms.Button btnValidateXML;
	}
}

