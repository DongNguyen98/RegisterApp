using Neurotec.Licensing;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace RegisterApp
{
    static class Program
    {
        private static Mutex mutex = null;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() 
        {


            //Check Neurotechnology licenses
            string address = "/local";
            string port = "5000";
            bool retry;
            string[] licenses =
            {
                "Biometrics.FingerExtraction",
                "Biometrics.FaceExtraction",
                "Biometrics.FingerMatchingFast",
                "Biometrics.FingerMatching",
                "Biometrics.FaceMatchingFast",
                "Biometrics.FaceMatching",
                "Biometrics.FingerQualityAssessment",
                "Biometrics.FingerSegmentation",
                "Biometrics.FingerSegmentsDetection",
                "Biometrics.FaceSegmentation",
                "Biometrics.Standards.Fingers",
                "Biometrics.Standards.FingerTemplates",
                "Biometrics.Standards.Faces",
                "Devices.Cameras",
                "Devices.FingerScanners",
                "Devices.Microphones",
                "Images.WSQ",
                "Media"
            };

            NLicenseManager.TrialMode = false;

            do
            {
                try
                {
                    retry = false;
                    foreach (string license in licenses)
                    {
                        NLicense.ObtainComponents(address, port, license);
                    }
                }
                catch (Exception ex)
                {
                    string message = string.Format("Failed to obtain licenses for components.\nError message: {0}", ex.Message);
                    if (ex is IOException)
                    {
                        message += "\n(Probably licensing service is not running. Use Activation Wizard to figure it out.)";
                    }
                    if (MessageBox.Show(message, @"Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry)
                    {
                        retry = true;
                    }
                    else
                    {
                        retry = false;
                        return;
                    }
                }
            }
            while (retry);

            //Start app in minimized state
            Utils.Logging("StartApp");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Main main = new Main
            {
                WindowState = FormWindowState.Minimized,
                ShowInTaskbar = false,
                Visible = false
            };
            Application.Run(main);
        }
    }
}