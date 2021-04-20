using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Comet1
{
    public partial class ToolBox : Form
    {
        private TextBox textBox1;
        private TextBox textBox2;
        private Button button1;
        private Button button3;
        private Button buttonConvert;

        //Timer 
        private Timer _timer;
        // The last time the timer was started
        private DateTime _startTime = DateTime.MinValue;
        // Time between now and when the timer was started last
        private TimeSpan _currentElapsedTime = TimeSpan.Zero;
        // Time between now and the first time timer was started after a reset
        private TimeSpan _totalElapsedTime = TimeSpan.Zero;
        private Label label1;

        // Whether or not the timer is currently running
        private bool _timerRunning = false;

        public ToolBox()
        {
            InitializeComponent();

            // Set up a timer and fire the Tick event
            _timer = new Timer();
            _timer.Interval = 100;
            _timer.Tick += new EventHandler(_timer_Tick);
        }

        private void InitializeComponent()
        {
            this.buttonConvert = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonConvert
            // 
            this.buttonConvert.Location = new System.Drawing.Point(230, 38);
            this.buttonConvert.Name = "buttonConvert";
            this.buttonConvert.Size = new System.Drawing.Size(75, 23);
            this.buttonConvert.TabIndex = 0;
            this.buttonConvert.Text = "Convert";
            this.buttonConvert.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(12, 40);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 20);
            this.textBox1.TabIndex = 1;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(118, 40);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(100, 20);
            this.textBox2.TabIndex = 2;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(32, 226);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "RESET";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(211, 226);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 3;
            this.button3.Text = "START";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Monospac821 BT", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(33, 178);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(262, 45);
            this.label1.TabIndex = 4;
            this.label1.Text = "0:00:00.000";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // ToolBox
            // 
            this.ClientSize = new System.Drawing.Size(317, 261);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.buttonConvert);
            this.Name = "ToolBox";
            this.Text = "COMET Tools";
            this.Load += new System.EventHandler(this.Convertor_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        void _timer_Tick(object sender, EventArgs e)
        {
            // We do this to chop off any stray milliseconds resulting from 
            // the Timer's inherent inaccuracy, with the bonus that the 
            // TimeSpan.ToString() method will now show correct HH:MM:SS format
            var timeSinceStartTime = DateTime.Now - _startTime;
            timeSinceStartTime = new TimeSpan(timeSinceStartTime.Days,
                                              timeSinceStartTime.Hours,
                                              timeSinceStartTime.Minutes,
                                              timeSinceStartTime.Seconds,
                                              timeSinceStartTime.Milliseconds);

            // The current elapsed time is the time since the start button was
            // clicked, plus the total time elapsed since the last reset
            _currentElapsedTime = timeSinceStartTime + _totalElapsedTime;
            
            // These are just two Label controls which display the current 
            // elapsed time and total elapsed time
            //label1.Text = timeSinceStartTime.ToString("g");

            label1.Text = _currentElapsedTime.ToString("g");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _totalElapsedTime = TimeSpan.Zero;
            _startTime = DateTime.Now;
            // fire the event to update the display
            _timer_Tick(this, new EventArgs());
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void Convertor_Load(object sender, EventArgs e)
        {
        }

        private void button3_Click(object sender, EventArgs e)
        {
            _timerRunning = !_timerRunning;
            if (_timerRunning)
            {
                _startTime = DateTime.Now;
                _timer.Start();
                button3.Text = "STOP";
            }
            else
            {
                _totalElapsedTime = _currentElapsedTime;
                _timer.Stop();
                button3.Text = "START";
            }
        }
    }
}
