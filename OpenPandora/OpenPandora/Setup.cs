using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using centrafuse.Plugins;
using System.Windows.Forms;

namespace OpenPandora
{
    public class Setup : CFSetup
    {
        internal const string PASSWORD = "f5e98aad-2466-492c-88a2-458a9db614f5";

        // The setup constructor will be called each time this plugin's setup is opened from the CF Setting Page
        // This setup is opened as a dialog from the CF_pluginShowSetup() call into the main plugin application form.
        public Setup(ICFMain mForm, ConfigReader config, LanguageReader lang)
        {
            // MainForm must be set before calling any Centrafuse API functions
            this.MainForm = mForm;

            // pluginConfig and pluginLang should be set before calling CF_initSetup() so this CFSetup instance 
            // will internally save any changed settings.
            this.pluginConfig = config;
            this.pluginLang = lang;

            // When CF_initSetup() is called, the CFPlugin layer will call back into CF_setupReadSettings() to read the page
            // Note that this.pluginConfig and this.pluginLang must be set before making this call
            CF_initSetup(1, 1);

            // Update the Settings page title
            this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/TITLE"));
        }

        public override void CF_setupReadSettings(int currentpage, bool advanced)
        {
            try
            {
                ButtonHandler[CFSetupButton.One] = new CFSetupHandler(SetEmail);
                ButtonText[CFSetupButton.One] = this.pluginLang.ReadField("/APPLANG/SETUP/EMAIL");
                ButtonValue[CFSetupButton.One] = this.pluginConfig.ReadField("/APPCONFIG/EMAIL");

                ButtonHandler[CFSetupButton.Two] = new CFSetupHandler(SetPassword);
                ButtonText[CFSetupButton.Two] = this.pluginLang.ReadField("/APPLANG/SETUP/PASSWORD");
                string encryptedPassword = this.pluginConfig.ReadField("/APPCONFIG/PASSWORD");
                ButtonValue[CFSetupButton.Two] = String.IsNullOrEmpty(encryptedPassword) ? String.Empty : new String('•', 8);

                ButtonHandler[CFSetupButton.Three] = new CFSetupHandler(SetBitrate);
                ButtonText[CFSetupButton.Three] = this.pluginLang.ReadField("/APPLANG/SETUP/BITRATE");
                ButtonValue[CFSetupButton.Three] = GetBitrateString();


            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void SetEmail(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type display name
                DialogResult dialogResult = this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/EMAIL"), ButtonValue[(int)value], null, out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1);

                if (dialogResult == DialogResult.OK)
                {
                    // save user value, note this does not save to file yet, as this should only be done when user confirms settings
                    // being overwritten when they click the "Save" button.  Saving is done internally by the CFSetup instance if
                    // pluginConfig and pluginLang were properly set before callin CF_initSetup().
                    this.pluginConfig.WriteField("/APPCONFIG/EMAIL", resultvalue);

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = resultvalue;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void SetPassword(ref object value)
        {
            try
            {
                object tempobject;
                string resultvalue, resulttext;

                // Display OSK for user to type password
                DialogResult dialogResult = this.CF_systemDisplayDialog(CF_Dialogs.OSK, this.pluginLang.ReadField("/APPLANG/SETUP/PASSWORD"), String.Empty, "PASSWORD", out resultvalue, out resulttext, out tempobject, null, true, true, true, true, false, false, 1);

                if (dialogResult == DialogResult.OK)
                {
                    // save user value, note this does not save to file yet, as this should only be done when user confirms settings
                    // being overwritten when they click the "Save" button.  Saving is done internally by the CFSetup instance if
                    // pluginConfig and pluginLang were properly set before callin CF_initSetup().
                    this.pluginConfig.WriteField("/APPCONFIG/PASSWORD", EncryptionHelper.EncryptString(resultvalue, PASSWORD));

                    // Display new value on Settings Screen button
                    ButtonValue[(int)value] = new String('•', 8);
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private string GetBitrateString()
        {
            string bitrateString = this.pluginConfig.ReadField("/APPCONFIG/BITRATE");
            if (string.IsNullOrEmpty(bitrateString))
            {
                return "MP3 128kbps";
            }
            else
            {
                switch (bitrateString)
                {
                    case "0": return "AAC+ 64kbps";
                    case "1": return "MP3 128kbps";
                    case "2": return "MP3 192kbps (Pandora One only)";
                }
                
            }
            return string.Empty;    
        }

        private void SetBitrate(ref object value)
        {
            try
            {
                string currentBitrate = GetBitrateString();

                CFControls.CFListViewItem[] audioFormatItems = new CFControls.CFListViewItem[3];
                audioFormatItems[0] = new CFControls.CFListViewItem("AAC+ 64kbps", "0", false);
                audioFormatItems[1] = new CFControls.CFListViewItem("MP3 128kbps", "1", false);
                audioFormatItems[2] = new CFControls.CFListViewItem("MP3 192kbps (Pandora One only)", "2", false);

                object resultObject;
                string resultvalue, resulttext;

                DialogResult result = this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser, this.pluginLang.ReadField("/APPLANG/SETUP/BITRATE"), currentBitrate, currentBitrate, out resultvalue, out resulttext, out resultObject, audioFormatItems, false, true, false, false, false, false, 1);

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    this.pluginConfig.WriteField("/APPCONFIG/BITRATE", resultvalue);
                    ButtonValue[(int)value] = resulttext;
                }

            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }
    }
}
