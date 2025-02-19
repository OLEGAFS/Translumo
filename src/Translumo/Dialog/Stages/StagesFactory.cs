﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translumo.Infrastructure.Powershell;
using Translumo.Infrastructure.Python;
using Translumo.Utils;

namespace Translumo.Dialog.Stages
{
    public static class StagesFactory
    {
        private const string EASYOCR_VERSION = "1.6.2";

        public static InteractionStage CreateLanguageChangeStages(DialogService dialogService, Action changeLangAction, ILogger logger)
        {
            return new ActionInteractionStage(dialogService, () => 
                        Task.Factory.StartNew(changeLangAction), LocalizationManager.GetValue($"Str.Stages.SwitchLanguage"))
                .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "Language change error"), LocalizationManager.GetValue("Str.Stages.SwitchLanguageError")));
        }

        public static InteractionStage CreateWindowsOcrCheckingStages(DialogService dialogService, string languageCode, InteractionStage enableFlagStage, ILogger logger)
        {
            return new ConditionalInteractionStage(dialogService,
                    () => OptionalFeaturesProvider.OcrLanguagePackIsInstalled(languageCode), LocalizationManager.GetValue("Str.Stages.CheckLangPack"))
                .AddNextFalse(new DialogQuestionInteractionStage(dialogService, string.Format(LocalizationManager.GetValue("Str.Stages.LangPackQuestion", true), languageCode))
                    .AddNextStage(new ConditionalInteractionStage(dialogService, async () => (await OptionalFeaturesProvider.OcrLanguagePackInstall(languageCode)).RestartIsNeeded, LocalizationManager.GetValue("Str.Stages.InstallationLangPack"))
                        .AddNextFalse(enableFlagStage)
                        .AddNextStage(new DialogInteractionStage(dialogService, LocalizationManager.GetValue("Str.Stages.LangPackInstalledRestart"))
                            .AddNextStage(enableFlagStage))
                        .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "Language windows pack install error"), LocalizationManager.GetValue("Str.Stages.InstallationLangError")))))
                .AddNextStage(enableFlagStage)
                .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "Checking language pack error"), LocalizationManager.GetValue("Str.Stages.CheckLangPackError", true)));
        }
        
        public static InteractionStage CreateEasyOcrCheckingStages(DialogService dialogService, InteractionStage enableFlagStage, ILogger logger)
        {
            return new ConditionalInteractionStage(dialogService,
                    async () => await PythonProvider.ModuleIsInstalledAsync("easyocr") && await PythonProvider.ModuleIsInstalledAsync("torch"), LocalizationManager.GetValue("Str.Stages.CheckPyModules"))
                .AddNextFalse(new DialogQuestionInteractionStage(dialogService, LocalizationManager.GetValue("Str.Stages.PyModulesQuestion", true))
                    .AddNextStage(new DialogQuestionInteractionStage(dialogService, LocalizationManager.GetValue("Str.Stages.PyModulesQuestion2", true))
                        .AddNextStage(new ActionInteractionStage(dialogService, () => PythonProvider.InstallModuleAsync("torch torchvision --index-url https://download.pytorch.org/whl/cu118"), LocalizationManager.GetValue("Str.Stages.InstallationPyModule1"))
                            .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "PyTorch installation error"), "{0}"))
                            .AddNextStage(new ActionInteractionStage(dialogService, () => PythonProvider.InstallModuleAsync($"easyocr=={EASYOCR_VERSION}"), LocalizationManager.GetValue("Str.Stages.InstallationPyModule2"))
                                .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "EasyOCR installation error"), "{0}"))
                                .AddNextStage(new DialogInteractionStage(dialogService, LocalizationManager.GetValue("Str.Stages.PyModulesInstalled"))
                                    .AddNextStage(enableFlagStage))))))
                .AddNextStage(enableFlagStage)
                .AddException(new ExceptionInteractionStage(dialogService, (ex) => logger.LogError(ex, "Easy OCR installation checking error"), LocalizationManager.GetValue("Str.Stages.PyModulesCheckError")));
        }
    }
}
