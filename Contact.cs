using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormsApp2
{
    public partial class Contact : SessionForm
    {
        private readonly Action<string> copyPhone;

        public Contact() : this(Clipboard.SetText) { }

        internal Contact(Action<string> copyPhone)
        {
            this.copyPhone = copyPhone ?? throw new ArgumentNullException(nameof(copyPhone));
            InitializeComponent();
            ArrangeContact();
        }

        private void back_Click(object sender, EventArgs e)
        {
            RequestBack();
        }
    }
}
