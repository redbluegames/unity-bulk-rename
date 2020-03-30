﻿/* MIT License

Copyright (c) 2020 Edward Rowe

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace RedBlueGames.MulliganRenamer
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// This class is responsible for getting, or retrieving, the up to date languages from the web.
    /// </summary>
    public class LanguageRetriever
    {
        public bool IsDoneUpdating { get; private set; }

        public LanguageRetriever()
        {
            this.IsDoneUpdating = true;
        }

        public void UpdateLanguages()
        {
            EditorCoroutineUtility.StartBackgroundTask(this.UpdateLanguagesAsync(), this.HandleUpdateComplete);
        }

        private IEnumerator UpdateLanguagesAsync()
        {
            EditorUtility.DisplayProgressBar("Updating Languages", "Checking for language updates...", 0.0f);
            this.IsDoneUpdating = false;

            LanguageBookmarks bookmarks = null;
            {
                var bookmarkRetriever = new JSONRetrieverWeb<LanguageBookmarks>
                    ("https://raw.githubusercontent.com/redbluegames/unity-mulligan-renamer/languages-from-web-tested/LanguageBookmarks.json");
                var bookmarkFetchOp = bookmarkRetriever.GetJSON(3);
                while (bookmarkFetchOp.Status == AsyncStatus.Pending)
                {
                    yield return null;
                }

                if (bookmarkFetchOp.Status != AsyncStatus.Success)
                {
                    ShowDisplayDialogForFailedOp(bookmarkFetchOp);
                    yield break;
                }

                bookmarks = bookmarkFetchOp.ResultData;
            }

            var languages = new List<Language>();
            {
                for (int i = 0; i < bookmarks.LanguageUrls.Count; ++i)
                {
                    var url = bookmarks.LanguageUrls[i];
                    var uri = new System.Uri(bookmarks.LanguageUrls[i]);
                    string filename = System.IO.Path.GetFileName(uri.LocalPath);

                    // Add one because we finished downloading Bookmarks.
                    var percentComplete = (i + 1) / (float)(bookmarks.LanguageUrls.Count + 1);
                    EditorUtility.DisplayProgressBar("Updating Languages", "Downloading language " + filename + "...", percentComplete);

                    var languageRetriever = new JSONRetrieverWeb<Language>(url);
                    var languageFetchOp = languageRetriever.GetJSON(3);
                    while (languageFetchOp.Status == AsyncStatus.Pending)
                    {
                        yield return null;
                    }

                    if (languageFetchOp.Status != AsyncStatus.Success)
                    {
                        ShowDisplayDialogForFailedOp(languageFetchOp);
                        yield break;
                    }

                    languages.Add(languageFetchOp.ResultData);
                }
            }

            EditorUtility.DisplayProgressBar("Updating Languages", "Saving Changes.", 1.0f);
            EditorUtility.ClearProgressBar();

            var reports = LocalizationManager.Instance.AddOrUpdateLanguages(languages);
            EditorUtility.DisplayDialog("Languages Successfully Updated", BuildDisplayStringForReport(reports), "OK");
        }

        private static void ShowDisplayDialogForFailedOp(AsyncOp op)
        {
            var message = BuildDisplayStringForAsyncOp(op);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Language Update Failed", message, "OK");
        }

        private static string BuildDisplayStringForAsyncOp(AsyncOp op)
        {
            string message = string.Empty;
            if (op.Status == AsyncStatus.Timeout)
            {
                message = "Update failed due to web request timeout. If you have internet, our servers may be down. " +
                    "Please try again later, or report a bug (see UserManual for details) if the issue persists.";
            }
            else if (op.Status == AsyncStatus.Failed)
            {
                message = string.Format(
                    "Update failed. Please report a bug (see UserManual for details). FailCode: {0}, Message: {1}",
                    op.FailureCode,
                    op.FailureMessage);
            }
            else
            {
                // Nothing to display for success or otherwise
            }

            return message;
        }

        private static string BuildDisplayStringForReport(List<LocalizationManager.LanguageUpdateReport> reports)
        {
            var updatedStringBuilder = new System.Text.StringBuilder();
            var addedLanguageStringBuilder = new System.Text.StringBuilder();
            var unchangedStringBuilder = new System.Text.StringBuilder();
            foreach (var report in reports)
            {
                if (report.Result == LocalizationManager.LanguageUpdateReport.UpdateResult.Updated)
                {
                    if (updatedStringBuilder.Length > 0)
                    {
                        updatedStringBuilder.AppendLine();
                    }

                    updatedStringBuilder.AppendFormat(
                        "Updated {0} from version {1} to {2}",
                        report.Language.Name,
                        report.PreviousVersion,
                        report.NewVersion);
                }
                else if (report.Result == LocalizationManager.LanguageUpdateReport.UpdateResult.Added)
                {
                    if (addedLanguageStringBuilder.Length > 0)
                    {
                        addedLanguageStringBuilder.AppendLine();
                    }

                    addedLanguageStringBuilder.AppendFormat("Added {0}.", report.Language.Name);
                }
                else
                {
                    if (unchangedStringBuilder.Length > 0)
                    {
                        unchangedStringBuilder.AppendLine();
                    }

                    unchangedStringBuilder.AppendFormat("{0} is up to date.", report.Language.Name);
                }
            }

            var message = new System.Text.StringBuilder();
            if (addedLanguageStringBuilder.Length > 0)
            {
                message.Append(addedLanguageStringBuilder);
            }

            if (updatedStringBuilder.Length > 0)
            {
                if (message.Length > 0)
                {
                    message.AppendLine();
                }

                message.Append(updatedStringBuilder);
            }

            if (message.Length == 0)
            {
                message.Append("All languages are up to date.");
            }
            else
            {
                message.AppendLine();
                message.Append(unchangedStringBuilder);
            }

            return message.ToString();
        }

        private void HandleUpdateComplete()
        {
            this.IsDoneUpdating = true;
        }
    }
}