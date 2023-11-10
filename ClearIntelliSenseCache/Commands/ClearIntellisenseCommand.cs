using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading.Tasks;
using OpenMcdf;
using System.Collections.Generic;

namespace ClearIntelliSenseCache
{
    [Command(PackageIds.ClearIntellisenseCommand)]
    internal sealed class ClearIntellisenseCommand : BaseCommand<ClearIntellisenseCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            bool confirmed = await VS.MessageBox.ShowConfirmAsync(Properties.Resources.ThisWillClearIntellisense, Properties.Resources.DoYouWantToContinue);
            if (confirmed)
            {
                await ClearCacheAndReloadSolutionAsync();
            }
        }


        private async Task ClearCacheAndReloadSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = this.Package.GetService<SDTE, EnvDTE80.DTE2>(true);
            if (dte != null)
            {
                string solutionFile = dte.Solution.FullName;
                string solutionSuoFile = await GetSolutionSuoFileAsync();
                if (solutionFile != null && solutionSuoFile != null)
                {
                    dte.Solution.Close(true);
                    ClearSolutionIntellisenseCache(solutionSuoFile);
                    dte.Solution.Open(solutionFile);
                }
            }
        }


        private async Task<string> GetSolutionSuoFileAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution = await this.Package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            Assumes.Present(solution);

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(solution.GetSolutionInfo(out string solutionDirectory, out string solutionFile, out string userOptsFile));
            return userOptsFile;
        }


        private bool ClearSolutionIntellisenseCache(string solutionSuoFile)
        {
            CompoundFile cf = new CompoundFile(solutionSuoFile,CFSUpdateMode.Update,CFSConfiguration.Default);

            var entriesToDelete = new List<string>();
            cf.RootStorage.VisitEntries((entry) => { 
                if (entry.IsStream && (entry.Name.EndsWith("_ProjState") || entry.Name.EndsWith("_TFMCaps")))
                {
                    entriesToDelete.Add(entry.Name);
                }
                }, false);

            if (entriesToDelete.Count > 0)
            {
                foreach (var entry in entriesToDelete)
                {
                    cf.RootStorage.Delete(entry);
                }
                cf.Commit();
                cf.Close();
                CompoundFile.ShrinkCompoundFile(solutionSuoFile);
                return true;
            }

            return false;
        }
    }
}
