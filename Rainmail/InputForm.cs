using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rainmail
{
    public partial class InputForm : Form
    {
        private TaskCompletionSource<string> task = new TaskCompletionSource<string>();

        public InputForm()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            task.TrySetResult(null);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            task.TrySetResult(passwordBox.Text);

            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        public static Task<string> QueryPassword()
        {
            InputForm form = new InputForm();
            form.ShowDialog();

            return form.Task;
        }

        // ----- Properties ----- //

        public Task<string> Task => task.Task;
    }
}
