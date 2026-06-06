using System;
using System.Collections.Generic;
using System.Globalization;

namespace LSOL.UI
{
    public enum ModLanguage
    {
        English = 0,
        French = 1,
        Italian = 2,
        Spanish = 3,
        Russian = 4,
        Portuguese = 8,
        Turkish = 9,
    }

    internal static partial class ModTextKey
    {
        public const string CommonOn = "common.on";
        public const string CommonOff = "common.off";
        public const string CommonBack = "common.back";
        public const string CommonClose = "common.close";
        public const string CommonCreate = "common.create";
        public const string CommonDelete = "common.delete";
        public const string CommonLoad = "common.load";
        public const string CommonSave = "common.save";

        public const string MenuGameModControlTitle = "menu.gameModControl.title";
        public const string MenuGameModControlSubtitle = "menu.gameModControl.subtitle";
        public const string MenuSavingOptionsTitle = "menu.savingOptions.title";
        public const string MenuSavingOptionsSubtitle = "menu.savingOptions.subtitle";
        public const string MenuDifficultyTitle = "menu.difficulty.title";
        public const string MenuDifficultySubtitle = "menu.difficulty.subtitle";
        public const string MenuDifficultyLockedSubtitle = "menu.difficulty.lockedSubtitle";
        public const string MenuDifficultyActionsTitle = "menu.difficulty.actions.title";
        public const string MenuDifficultyActionsSubtitle = "menu.difficulty.actions.subtitle";
        public const string MenuDifficultyTemplateLoadTitle = "menu.difficulty.templateLoad.title";
        public const string MenuDifficultyTemplateLoadSubtitle = "menu.difficulty.templateLoad.subtitle";
        public const string MenuNewSaveSetupSubtitle = "menu.newSaveSetup.subtitle";
        public const string MenuSaveSlotsLoadTitle = "menu.saveSlots.loadTitle";
        public const string MenuSaveSlotsDeleteTitle = "menu.saveSlots.deleteTitle";
        public const string MenuSaveSlotsSingle = "menu.saveSlots.single";
        public const string MenuSaveSlotsMany = "menu.saveSlots.many";
        public const string MenuOptionsTitle = "menu.options.title";
        public const string MenuOptionsSubtitle = "menu.options.subtitle";

        public const string RowSavingOptions = "row.savingOptions";
        public const string RowDifficultySettings = "row.difficultySettings";
        public const string RowOptions = "row.options";
        public const string RowCreateNewSave = "row.createNewSave";
        public const string RowLoadSave = "row.loadSave";
        public const string RowDeleteSave = "row.deleteSave";
        public const string RowSaveGame = "row.saveGame";
        public const string RowCreateSave = "row.createSave";
        public const string RowNoSavesFound = "row.noSavesFound";
        public const string RowLanguage = "row.language";
        public const string RowSpeedUnit = "row.speedUnit";
        public const string RowColorblindMode = "row.colorblindMode";
        public const string RowVehicleFuel = "row.vehicleFuel";
        public const string RowCargoWeightPower = "row.cargoWeightPower";
        public const string RowCruiseControl = "row.cruiseControl";
        public const string RowCargoDamage = "row.cargoDamage";
        public const string RowIndustryPriceMechanic = "row.industryPriceMechanic";
        public const string RowLicensingSystem = "row.licensingSystem";
        public const string RowCorridorRestriction = "row.corridorRestriction";
        public const string RowReputationSystem = "row.reputationSystem";
        public const string RowOfficeGarageLimit = "row.officeGarageLimit";
        public const string RowOfficeNpcLimit = "row.officeNpcLimit";
        public const string RowNpcRouteLimit = "row.npcRouteLimit";
        public const string RowDifficultyTemplatesActions = "row.difficulty.templatesActions";
        public const string RowDifficultyEnableAll = "row.difficulty.enableAll";
        public const string RowDifficultyDisableAll = "row.difficulty.disableAll";
        public const string RowDifficultySaveTemplate = "row.difficulty.saveTemplate";
        public const string RowDifficultyLoadTemplate = "row.difficulty.loadTemplate";
        public const string RowNoDifficultyTemplates = "row.difficulty.noTemplates";
        public const string RowEconomyPreset = "row.economyPreset";
        public const string RowNpcWeeklyWages = "row.npcWeeklyWages";
        public const string RowStartingBalance = "row.startingBalance";
        public const string RowStartingGuides = "row.startingGuides";
        public const string RowActivate = "row.activate";

        public const string DetailSavingOptions = "detail.savingOptions";
        public const string DetailDifficultyUnlocked = "detail.difficulty.unlocked";
        public const string DetailDifficultyLocked = "detail.difficulty.locked";
        public const string DetailCreateNewSave = "detail.createNewSave";
        public const string DetailLoadSave = "detail.loadSave";
        public const string DetailDeleteSave = "detail.deleteSave";
        public const string DetailSaveGame = "detail.saveGame";
        public const string DetailNeedNamedSave = "detail.needNamedSave";
        public const string DetailNoSavesFound = "detail.noSavesFound";
        public const string DetailNewSaveStartingBalance = "detail.newSave.startingBalance";
        public const string DetailStartingGuides = "detail.startingGuides";
        public const string DetailSpeedUnit = "detail.speedUnit";
        public const string DetailVehicleFuel = "detail.vehicleFuel";
        public const string DetailCargoWeightPower = "detail.cargoWeightPower";
        public const string DetailCruiseControl = "detail.cruiseControl";
        public const string DetailCargoDamage = "detail.cargoDamage";
        public const string DetailIndustryPriceMechanic = "detail.industryPriceMechanic";
        public const string DetailLicensingSystem = "detail.licensingSystem";
        public const string DetailCorridorRestriction = "detail.corridorRestriction";
        public const string DetailReputationSystem = "detail.reputationSystem";
        public const string DetailOfficeGarageLimit = "detail.officeGarageLimit";
        public const string DetailOfficeNpcLimitEnabled = "detail.officeNpcLimit.enabled";
        public const string DetailOfficeNpcLimitDisabled = "detail.officeNpcLimit.disabled";
        public const string DetailNpcRouteLimit = "detail.npcRouteLimit";
        public const string DetailCreateSave = "detail.createSave";
        public const string DetailLanguage = "detail.language";
        public const string DetailLanguageEnglishFallback = "detail.language.englishFallback";
        public const string DetailSpeedUnitChanged = "detail.speedUnitChanged";
        public const string DetailColorblindMode = "detail.colorblindMode";
        public const string DetailOptions = "detail.options";
        public const string DetailOptionsNavigate = "detail.options.navigate";
        public const string DetailDifficultySettingsLockedStatus = "detail.difficulty.lockedStatus";
        public const string DetailDifficultyTemplatesActions = "detail.difficulty.templatesActions";
        public const string DetailDifficultyTemplatesActionsLocked = "detail.difficulty.templatesActionsLocked";
        public const string DetailDifficultyEnableAll = "detail.difficulty.enableAll";
        public const string DetailDifficultyDisableAll = "detail.difficulty.disableAll";
        public const string DetailDifficultyBulkActionBlockedLocked = "detail.difficulty.bulkActionBlockedLocked";
        public const string DetailDifficultySaveTemplate = "detail.difficulty.saveTemplate";
        public const string DetailDifficultyLoadTemplate = "detail.difficulty.loadTemplate";
        public const string DetailDifficultyNoTemplatesFound = "detail.difficulty.noTemplatesFound";
        public const string DetailDifficultyTemplateSaved = "detail.difficulty.templateSaved";
        public const string DetailDifficultyTemplateOverwritten = "detail.difficulty.templateOverwritten";
        public const string DetailDifficultyTemplateLoaded = "detail.difficulty.templateLoaded";
        public const string DetailDifficultyTemplateSaveCancelled = "detail.difficulty.templateSaveCancelled";
        public const string DetailDifficultyTemplateNameInvalid = "detail.difficulty.templateNameInvalid";
        public const string DetailDifficultyTemplateBlockedLocked = "detail.difficulty.templateBlockedLocked";
        public const string DetailPersistenceEnabled = "detail.persistence.enabled";
        public const string DetailPersistenceDisabled = "detail.persistence.disabled";
        public const string DetailDebugUnavailable = "detail.debug.unavailable";
        public const string DetailLoadSavedState = "detail.loadSavedState";
        public const string DetailLoadSavedSettings = "detail.loadSavedSettings";
        public const string DetailNoSavedState = "detail.noSavedState";
        public const string DetailNoSavePath = "detail.noSavePath";
        public const string DetailSaveCreationCancelled = "detail.saveCreationCancelled";
        public const string DetailSaveNameInvalid = "detail.saveNameInvalid";
        public const string DetailSaveExists = "detail.saveExists";
        public const string DetailNoPendingSaveName = "detail.noPendingSaveName";
        public const string DetailSelectedSaveMissing = "detail.selectedSaveMissing";
        public const string DetailCreateOrLoadNamedSave = "detail.createOrLoadNamedSave";
        public const string DetailSaveCreated = "detail.saveCreated";
        public const string DetailSaveLoaded = "detail.saveLoaded";
        public const string DetailSaveDeleted = "detail.saveDeleted";
        public const string DetailSaveSaved = "detail.saveSaved";
        public const string DetailLoadedSuccessfully = "detail.loadedSuccessfully";
        public const string DetailMechanicsEnabled = "detail.mechanicsEnabled";
        public const string DetailMechanicsDisabled = "detail.mechanicsDisabled";
        public const string DetailLanguageChanged = "detail.languageChanged";
        public const string DetailColorblindChanged = "detail.colorblindChanged";
        public const string DetailEconomyPresetCasual = "detail.economyPreset.casual";
        public const string DetailEconomyPresetStandard = "detail.economyPreset.standard";
        public const string DetailEconomyPresetHardcore = "detail.economyPreset.hardcore";
        public const string DetailEconomyPresetImpossible = "detail.economyPreset.impossible";
        public const string DetailNpcWeeklyPayroll = "detail.npcWeeklyPayroll";

        public const string LemonToggleHint = "lemon.toggleHint";
        public const string LemonAdjustHint = "lemon.adjustHint";
        public const string LemonCycleHint = "lemon.cycleHint";
        public const string LemonUseHint = "lemon.useHint";

        public const string ValueDefaultAutosave = "value.defaultAutosave";
        public const string ValueDifficultyCasual = "value.difficulty.casual";
        public const string ValueDifficultyStandard = "value.difficulty.standard";
        public const string ValueDifficultyHardcore = "value.difficulty.hardcore";
        public const string ValueDifficultyImpossible = "value.difficulty.impossible";

        public const string ValueLanguageEnglishFallback = "value.language.englishFallback";
        public const string ValueLanguageFrench = "value.language.french";
        public const string ValueLanguageItalian = "value.language.italian";
        public const string ValueLanguageSpanish = "value.language.spanish";
        public const string ValueLanguageRussian = "value.language.russian";
        public const string ValueLanguagePortuguese = "value.language.portuguese";
        public const string ValueLanguageTurkish = "value.language.turkish";

        public const string ValueUnitImperial = "value.unit.imperial";
        public const string ValueUnitMetric = "value.unit.metric";

        public const string ValueColorblindOff = "value.colorblind.off";
        public const string ValueColorblindDeuteranopia = "value.colorblind.deuteranopia";
        public const string ValueColorblindProtanopia = "value.colorblind.protanopia";
        public const string ValueColorblindTritanopia = "value.colorblind.tritanopia";
    }

    internal sealed class ModLocalizationService
    {
        private static readonly Dictionary<string, Dictionary<ModLanguage, string>> Translations = BuildTranslations();

        public ModLocalizationService()
        {
            Language = ModLanguage.English;
        }

        public ModLanguage Language { get; private set; }

        public bool SetLanguage(ModLanguage language)
        {
            if (Language == language)
            {
                return false;
            }

            Language = language;
            return true;
        }

        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            Dictionary<ModLanguage, string> entry;
            if (!Translations.TryGetValue(key, out entry) || entry == null)
            {
                return key;
            }

            string value;
            if (entry.TryGetValue(Language, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (entry.TryGetValue(ModLanguage.English, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return key;
        }

        public string Format(string key, params object[] args)
        {
            var format = Get(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private static Dictionary<string, Dictionary<ModLanguage, string>> BuildTranslations()
        {
            var translations = new Dictionary<string, Dictionary<ModLanguage, string>>(StringComparer.Ordinal);

            Add(translations,  ModTextKey.CommonOn,  "On",  "Activé",  "Attivo",  "Activado",  "Вкл.",  "Ligado",  "Açık");
            Add(translations,  ModTextKey.CommonOff,  "Off",  "Désactivé",  "Disattivato",  "Desactivado",  "Выкл.",  "Desligado",  "Kapalı");
            Add(translations,  ModTextKey.CommonBack,  "Back",  "Retour",  "Indietro",  "Atrás",  "Назад",  "Voltar",  "Geri");
            Add(translations,  ModTextKey.CommonClose,  "Close",  "Fermer",  "Chiudi",  "Cerrar",  "Закрыть",  "Fechar",  "Kapat");
            Add(translations,  ModTextKey.CommonCreate,  "Create",  "Créer",  "Crea",  "Crear",  "Создать",  "Criar",  "Oluştur");
            Add(translations,  ModTextKey.CommonDelete,  "Delete",  "Supprimer",  "Elimina",  "Eliminar",  "Удалить",  "Excluir",  "Sil");
            Add(translations,  ModTextKey.CommonLoad,  "Load",  "Charger",  "Carica",  "Cargar",  "Загрузить",  "Carregar",  "Yükle");
            Add(translations,  ModTextKey.CommonSave,  "Save",  "Sauvegarder",  "Salva",  "Guardar",  "Сохранить",  "Salvar",  "Kaydet");

            Add(translations,  ModTextKey.MenuGameModControlTitle,  "Game Mod Control",  "Contrôle du mod",  "Controllo mod",  "Control del mod",  "Управление модом",  "Controle do mod",  "Mod kontrolü");
            Add(translations,  ModTextKey.MenuGameModControlSubtitle,  "Activate mechanics and configure gameplay",  "Configurer le gameplay",  "Attiva le meccaniche e configura il gameplay",  "Activa las mecánicas y configura la partida",  "Включайте механики и настраивайте игровой процесс",  "Ative mecânicas e configure a jogabilidade",  "Mekanikleri açın ve oynanışı ayarlayın");
            Add(translations,  ModTextKey.MenuSavingOptionsTitle,  "Saving Options",  "Options de sauvegarde",  "Opzioni di salvataggio",  "Opciones de guardado",  "Параметры сохранения",  "Opções de salvamento",  "Kayıt seçenekleri");
            Add(translations,  ModTextKey.MenuSavingOptionsSubtitle,  "Active save: {0}",  "Sauvegarde active : {0}",  "Salvataggio attivo: {0}",  "Partida activa: {0}",  "Активное сохранение: {0}",  "Save ativo: {0}",  "Etkin kayıt: {0}");
            Add(translations,  ModTextKey.MenuDifficultyTitle,  "Difficulty Settings",  "Paramètres de difficulté",  "Impostazioni difficoltà",  "Ajustes de dificultad",  "Настройки сложности",  "Configurações de dificuldade",  "Zorluk ayarları");
            Add(translations,  ModTextKey.MenuDifficultySubtitle,  "Enable or disable challenge options",  "Activer ou désactiver les options de défi",  "Attiva o disattiva le opzioni di sfida",  "Activa o desactiva opciones de desafío",  "Включайте и отключайте параметры испытания",  "Ative ou desative opções de desafio",  "Meydan okuma seçeneklerini açıp kapatın");
            Add(translations,  ModTextKey.MenuDifficultyLockedSubtitle,  "Locked by the active save. Values are read-only.",  "Verrouillé par la sauvegarde active. Valeurs en lecture seule.",  "Bloccato dal salvataggio attivo. Valori in sola lettura.",  "Bloqueado por la partida activa. Valores de solo lectura.",  "Заблокировано активным сохранением. Значения только для чтения.",  "Bloqueado pelo save ativo. Valores somente leitura.",  "Etkin kayıt tarafından kilitlendi. Değerler salt okunur.");
            Add(translations,  ModTextKey.MenuDifficultyActionsTitle,  "Difficulty Templates",  "Difficulty Templates",  "Difficulty Templates",  "Difficulty Templates",  "Difficulty Templates",  "Difficulty Templates",  "Difficulty Templates");
            Add(translations,  ModTextKey.MenuDifficultyActionsSubtitle,  "Save the current profile",  "Save the current profile or load a reusable full profile.",  "Save the current profile or load a reusable full profile.",  "Save the current profile or load a reusable full profile.",  "Save the current profile or load a reusable full profile.",  "Save the current profile or load a reusable full profile.",  "Save the current profile or load a reusable full profile.");
            Add(translations,  ModTextKey.MenuDifficultyTemplateLoadTitle,  "Load Difficulty Template",  "Load Difficulty Template",  "Load Difficulty Template",  "Load Difficulty Template",  "Load Difficulty Template",  "Load Difficulty Template",  "Load Difficulty Template");
            Add(translations,  ModTextKey.MenuDifficultyTemplateLoadSubtitle,  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.",  "Choose a saved full difficulty profile.");
            Add(translations,  ModTextKey.MenuNewSaveSetupSubtitle,  "Configure '{0}'",  "Configurez '{0}' avant de commencer",  "Configura '{0}' prima di iniziare",  "Configura '{0}' antes de empezar",  "Настройте '{0}' перед стартом",  "Configure '{0}' antes de começar",  "Başlamadan önce '{0}' yapılandırın");
            Add(translations,  ModTextKey.MenuSaveSlotsLoadTitle,  "Load save",  "Charger une sauvegarde",  "Carica salvataggio",  "Cargar partida",  "Загрузить сохранение",  "Carregar save",  "Kaydı yükle");
            Add(translations,  ModTextKey.MenuSaveSlotsDeleteTitle,  "Delete save",  "Supprimer une sauvegarde",  "Elimina salvataggio",  "Eliminar partida",  "Удалить сохранение",  "Excluir save",  "Kaydı sil");
            Add(translations,  ModTextKey.MenuSaveSlotsSingle,  "1 savegame found",  "1 sauvegarde trouvée",  "1 salvataggio trovato",  "1 partida encontrada",  "Найдено 1 сохранение",  "1 save encontrado",  "1 kayıt bulundu");
            Add(translations,  ModTextKey.MenuSaveSlotsMany,  "{0} savegames found",  "{0} sauvegardes trouvées",  "{0} salvataggi trovati",  "{0} partidas encontradas",  "Найдено сохранений: {0}",  "{0} saves encontrados",  "{0} kayıt bulundu");
            Add(translations,  ModTextKey.MenuOptionsTitle,  "Options",  "Options",  "Opzioni",  "Opciones",  "Параметры",  "Opções",  "Seçenekler");
            Add(translations,  ModTextKey.MenuOptionsSubtitle,  "Language, units, and accessibility settings",  "Langue et accessibilité",  "Lingua e accessibilità",  "Idioma y accesibilidad",  "Язык и специальные возможности",  "Idioma e acessibilidade",  "Dil ve erişilebilirlik ayarları");

            Add(translations,  ModTextKey.RowSavingOptions,  "Saving Options",  "Options de sauvegarde",  "Opzioni di salvataggio",  "Opciones de guardado",  "Параметры сохранения",  "Opções de salvamento",  "Kayıt seçenekleri");
            Add(translations,  ModTextKey.RowDifficultySettings,  "Difficulty settings",  "Paramètres de difficulté",  "Impostazioni difficoltà",  "Ajustes de dificultad",  "Настройки сложности",  "Configurações de dificuldade",  "Zorluk ayarları");
            Add(translations,  ModTextKey.RowOptions,  "Options",  "Options",  "Opzioni",  "Opciones",  "Параметры",  "Opções",  "Seçenekler");
            Add(translations,  ModTextKey.RowCreateNewSave,  "Create new save",  "Créer une nouvelle sauvegarde",  "Crea nuovo salvataggio",  "Crear nueva partida",  "Создать новое сохранение",  "Criar novo save",  "Yeni kayıt oluştur");
            Add(translations,  ModTextKey.RowLoadSave,  "Load save",  "Charger une sauvegarde",  "Carica salvataggio",  "Cargar partida",  "Загрузить сохранение",  "Carregar save",  "Kaydı yükle");
            Add(translations,  ModTextKey.RowDeleteSave,  "Delete save",  "Supprimer une sauvegarde",  "Elimina salvataggio",  "Eliminar partida",  "Удалить сохранение",  "Excluir save",  "Kaydı sil");
            Add(translations,  ModTextKey.RowSaveGame,  "Save game",  "Sauvegarder la partie",  "Salva partita",  "Guardar partida",  "Сохранить игру",  "Salvar jogo",  "Oyunu kaydet");
            Add(translations,  ModTextKey.RowCreateSave,  "Create save",  "Créer la sauvegarde",  "Crea salvataggio",  "Crear partida",  "Создать сохранение",  "Criar save",  "Kaydı oluştur");
            Add(translations,  ModTextKey.RowNoSavesFound,  "No saves found",  "Aucune sauvegarde trouvée",  "Nessun salvataggio trovato",  "No se encontraron partidas",  "Сохранения не найдены",  "Nenhum save encontrado",  "Kayıt bulunamadı");
            Add(translations,  ModTextKey.RowLanguage,  "Language: < {0} >",  "Langue : < {0} >",  "Lingua: < {0} >",  "Idioma: < {0} >",  "Язык: < {0} >",  "Idioma: < {0} >",  "Dil: < {0} >");
            Add(translations,  ModTextKey.RowSpeedUnit,  "Speed units: < {0} >",  "Unités de vitesse : < {0} >",  "Unità velocità: < {0} >",  "Unidades de velocidad: < {0} >",  "Единицы скорости: < {0} >",  "Unidades de velocidade: < {0} >",  "Hız birimleri: < {0} >");
            Add(translations,  ModTextKey.RowColorblindMode,  "Colorblind mode: < {0} >",  "Mode daltonien : < {0} >",  "Modalità daltonismo: < {0} >",  "Modo daltónico: < {0} >",  "Режим дальтонизма: < {0} >",  "Modo daltônico: < {0} >",  "Renk körü modu: < {0} >");
            Add(translations,  ModTextKey.RowVehicleFuel,  "Vehicle fuel",  "Carburant véhicule",  "Carburante veicolo",  "Combustible del vehículo",  "Топливо транспорта",  "Combustível do veículo",  "Araç yakıtı");
            Add(translations,  ModTextKey.RowCargoWeightPower,  "Cargo weight power",  "Cargo weight power",  "Cargo weight power",  "Cargo weight power",  "Cargo weight power",  "Cargo weight power",  "Cargo weight power");
            Add(translations,  ModTextKey.RowCruiseControl,  "Cruise control: {0}",  "Cruise control: {0}",  "Cruise control: {0}",  "Cruise control: {0}",  "Cruise control: {0}",  "Cruise control: {0}",  "Cruise control: {0}");
            Add(translations,  ModTextKey.RowCargoDamage,  "Cargo damage",  "Dégâts de cargaison",  "Danni al carico",  "Daño de la carga",  "Повреждение груза",  "Dano da carga",  "Yük hasarı");
            Add(translations,  ModTextKey.RowIndustryPriceMechanic,  "Industry price mechanic",  "Mécanique de prix d'industrie",  "Meccanica prezzo industria",  "Mecánica de precio industrial",  "Механика стоимости отрасли",  "Mecânica de preço da indústria",  "Endüstri fiyat mekaniği");
            Add(translations,  ModTextKey.RowLicensingSystem,  "Licensing system",  "Système de licence",  "Sistema di licenze",  "Sistema de licencias",  "Система лицензий",  "Sistema de licenças",  "Lisans sistemi");
            Add(translations,  ModTextKey.RowCorridorRestriction,  "Corridor restriction",  "Restriction de corridor",  "Restrizione corridoi",  "Restricción de corredores",  "Ограничение коридоров",  "Restrição de corredores",  "Koridor kısıtlaması");
            Add(translations,  ModTextKey.RowReputationSystem,  "Reputation system",  "Système de réputation",  "Sistema reputazione",  "Sistema de reputación",  "Система репутации",  "Sistema de reputação",  "İtibar sistemi");
            Add(translations,  ModTextKey.RowOfficeGarageLimit,  "Office garage limit",  "Limite garage bureau",  "Limite garage ufficio",  "Límite de garaje de oficina",  "Лимит гаража офиса",  "Limite da garagem do escritório",  "Ofis garaj sınırı");
            Add(translations,  ModTextKey.RowOfficeNpcLimit,  "NPC limit at office",  "NPC limit at office",  "NPC limit at office",  "NPC limit at office",  "NPC limit at office",  "NPC limit at office",  "NPC limit at office");
            Add(translations,  ModTextKey.RowNpcRouteLimit,  "NPC route limit: < {0} >",  "Limite de routes PNJ : < {0} >",  "Limite rotte NPC: < {0} >",  "Límite de rutas NPC: < {0} >",  "Лимит маршрутов NPC: < {0} >",  "Limite de rotas NPC: < {0} >",  "NPC rota sınırı: < {0} >");
            Add(translations,  ModTextKey.RowDifficultyTemplatesActions,  "Difficulty templates",  "Difficulty templates",  "Difficulty templates",  "Difficulty templates",  "Difficulty templates",  "Difficulty templates",  "Difficulty templates");
            Add(translations,  ModTextKey.RowDifficultyEnableAll,  "Enable all",  "Enable all",  "Enable all",  "Enable all",  "Enable all",  "Enable all",  "Enable all");
            Add(translations,  ModTextKey.RowDifficultyDisableAll,  "Disable all",  "Disable all",  "Disable all",  "Disable all",  "Disable all",  "Disable all",  "Disable all");
            Add(translations,  ModTextKey.RowDifficultySaveTemplate,  "Save template",  "Save template",  "Save template",  "Save template",  "Save template",  "Save template",  "Save template");
            Add(translations,  ModTextKey.RowDifficultyLoadTemplate,  "Load template",  "Load template",  "Load template",  "Load template",  "Load template",  "Load template",  "Load template");
            Add(translations,  ModTextKey.RowNoDifficultyTemplates,  "No templates saved",  "No templates saved",  "No templates saved",  "No templates saved",  "No templates saved",  "No templates saved",  "No templates saved");
            Add(translations,  ModTextKey.RowEconomyPreset,  "Economy preset: < {0} >",  "Préréglage économie : < {0} >",  "Preset economia: < {0} >",  "Preajuste económico: < {0} >",  "Экономика: < {0} >",  "Predefinição econômica: < {0} >",  "Ekonomi ön ayarı: < {0} >");
            Add(translations,  ModTextKey.RowNpcWeeklyWages,  "NPC weekly wages: < {0} >",  "Salaires hebdo PNJ : < {0} >",  "Salari settimanali NPC: < {0} >",  "Sueldos semanales NPC: < {0} >",  "Недельная зарплата NPC: < {0} >",  "Salários semanais dos NPCs: < {0} >",  "NPC haftalık ücretleri: < {0} >");
            Add(translations,  ModTextKey.RowStartingBalance,  "Starting balance: {0}",  "Solde initial : {0}",  "Saldo iniziale: {0}",  "Saldo inicial: {0}",  "Стартовый баланс: {0}",  "Saldo inicial: {0}",  "Başlangıç bakiyesi: {0}");
            Add(translations,  ModTextKey.RowStartingGuides,  "Starting guides",  "Starting guides",  "Starting guides",  "Starting guides",  "Starting guides",  "Starting guides",  "Starting guides");
            Add(translations,  ModTextKey.RowActivate,  "Activate: {0}",  "Activer : {0}",  "Attiva: {0}",  "Activar: {0}",  "Активировать: {0}",  "Ativar: {0}",  "Etkinleştir: {0}");

            Add(translations,  ModTextKey.DetailSavingOptions,  "Create, load, delete, or save named games. Active: {0}.",  "Créer, charger, supprimer ou sauvegarder des parties nommées. Active : {0}.",  "Crea, carica, elimina o salva partite nominate. Attiva: {0}.",  "Crea, carga, elimina o guarda partidas con nombre. Activa: {0}.",  "Создавайте, загружайте, удаляйте и сохраняйте именованные игры. Активно: {0}.",  "Crie, carregue, exclua ou salve jogos nomeados. Ativo: {0}.",  "Adlandırılmış kayıtlar oluşturun, yükleyin, silin veya kaydedin. Etkin: {0}.");
            Add(translations,  ModTextKey.DetailDifficultyUnlocked,  "Review the full gameplay difficulty profile. Economy: {0} | NPC wages: {1} | routes: {2} | challenge rules on: {3}.",  "Consultez le profil complet de difficulté. Économie : {0} | salaires PNJ : {1} | routes : {2} | règles actives : {3}.",  "Controlla il profilo difficoltà completo. Economia: {0} | salari NPC: {1} | rotte: {2} | regole attive: {3}.",  "Revisa el perfil completo de dificultad. Economía: {0} | salarios NPC: {1} | rutas: {2} | reglas activas: {3}.",  "Просмотрите полный профиль сложности. Экономика: {0} | зарплаты NPC: {1} | маршруты: {2} | активные правила: {3}.",  "Revise o perfil completo de dificuldade. Economia: {0} | salários de NPCs: {1} | rotas: {2} | regras ativas: {3}.",  "Tam zorluk profilini inceleyin. Ekonomi: {0} | NPC ücretleri: {1} | rotalar: {2} | açık kurallar: {3}.");
            Add(translations,  ModTextKey.DetailDifficultyLocked,  "Locked by the active save. Economy: {0} | NPC wages: {1} | routes: {2} | challenge rules on: {3}. Create a new save to change these settings.",  "Verrouillé par la sauvegarde active. Économie : {0} | salaires PNJ : {1} | routes : {2} | règles actives : {3}. Créez une nouvelle sauvegarde pour modifier ces paramètres.",  "Bloccato dal salvataggio attivo. Economia: {0} | salari NPC: {1} | rotte: {2} | regole attive: {3}. Crea un nuovo salvataggio per cambiare queste impostazioni.",  "Bloqueado por la partida activa. Economía: {0} | salarios NPC: {1} | rutas: {2} | reglas activas: {3}. Crea una nueva partida para cambiar estos ajustes.",  "Заблокировано активным сохранением. Экономика: {0} | зарплаты NPC: {1} | маршруты: {2} | активные правила: {3}. Создайте новое сохранение, чтобы изменить параметры.",  "Bloqueado pelo save ativo. Economia: {0} | salários de NPCs: {1} | rotas: {2} | regras ativas: {3}. Crie um novo save para alterar essas configurações.",  "Etkin kayıt tarafından kilitlendi. Ekonomi: {0} | NPC ücretleri: {1} | rotalar: {2} | açık kurallar: {3}. Bu ayarları değiştirmek için yeni kayıt oluşturun.");
            Add(translations,  ModTextKey.DetailCreateNewSave,  "Creates {0}.state.xml in {1}",  "Crée {0}.state.xml dans {1}",  "Crea {0}.state.xml in {1}",  "Crea {0}.state.xml en {1}",  "Создаёт {0}.state.xml в {1}",  "Cria {0}.state.xml em {1}",  "{1} içinde {0}.state.xml oluşturur");
            Add(translations,  ModTextKey.DetailLoadSave,  "Choose from all created savegames.",  "Choisissez parmi toutes les sauvegardes créées.",  "Scegli tra tutti i salvataggi creati.",  "Elige entre todas las partidas creadas.",  "Выберите одно из созданных сохранений.",  "Escolha entre todos os saves criados.",  "Oluşturulan tüm kayıtlardan seçin.");
            Add(translations,  ModTextKey.DetailDeleteSave,  "Delete one of your created savegames.",  "Supprimez une de vos sauvegardes.",  "Elimina uno dei tuoi salvataggi.",  "Elimina una de tus partidas creadas.",  "Удалите одно из созданных сохранений.",  "Exclua um dos seus saves criados.",  "Oluşturduğunuz kayıtlardan birini silin.");
            Add(translations,  ModTextKey.DetailSaveGame,  "Writes current progress to {0}.state.xml.",  "Écrit la progression actuelle dans {0}.state.xml.",  "Scrive i progressi correnti in {0}.state.xml.",  "Escribe el progreso actual en {0}.state.xml.",  "Записывает текущий прогресс в {0}.state.xml.",  "Grava o progresso atual em {0}.state.xml.",  "Geçerli ilerlemeyi {0}.state.xml dosyasına yazar.");
            Add(translations,  ModTextKey.DetailNeedNamedSave,  "Create or load a named save first.",  "Créez ou chargez d'abord une sauvegarde nommée.",  "Crea o carica prima un salvataggio con nome.",  "Crea o carga primero una partida con nombre.",  "Сначала создайте или загрузите именованное сохранение.",  "Crie ou carregue um save nomeado primeiro.",  "Önce adlandırılmış bir kayıt oluşturun veya yükleyin.");
            Add(translations,  ModTextKey.DetailNoSavesFound,  "Create a save in {0} first.",  "Créez d'abord une sauvegarde dans {0}.",  "Crea prima un salvataggio in {0}.",  "Primero crea una partida en {0}.",  "Сначала создайте сохранение в {0}.",  "Crie primeiro um save em {0}.",  "Önce {0} içinde kayıt oluşturun.");
            Add(translations,  ModTextKey.DetailNewSaveStartingBalance,  "Choose the opening balance for the new save.",  "Choisissez le solde de départ pour la nouvelle sauvegarde.",  "Scegli il saldo iniziale per il nuovo salvataggio.",  "Elige el saldo inicial para la nueva partida.",  "Выберите стартовый баланс для нового сохранения.",  "Escolha o saldo inicial do novo save.",  "Yeni kayıt için başlangıç bakiyesini seçin.");
            Add(translations,  ModTextKey.DetailStartingGuides,  "Starting guides are series of steps to help new players navigate in this complex mod. Enable it will add you a checklist to do at the start of your new save",  "Enable the guided startup checklist for this new save.",  "Enable the guided startup checklist for this new save.",  "Enable the guided startup checklist for this new save.",  "Enable the guided startup checklist for this new save.",  "Enable the guided startup checklist for this new save.",  "Enable the guided startup checklist for this new save.");
            Add(translations,  ModTextKey.DetailSpeedUnit,  "Switch the cruise-control speed display between imperial and metric units.",  "Bascule l'affichage du régulateur entre impérial et métrique.",  "Cambia l'affichage del cruise control tra imperiale e metrico.",  "Cambia el indicador del control de crucero entre imperial y métrico.",  "Переключает показ круиз-контроля между имперскими и метрическими единицами.",  "Troca a exibição do controle de cruzeiro entre imperial e métrico.",  "Hız sabitleyici göstergesini imperial ve metrik arasında değiştirir.");
            Add(translations,  ModTextKey.DetailVehicleFuel,  "Enable vehicle fuel usage for cargo operations.",  "Active l'usage du carburant pour les opérations cargo.",  "Abilita il carburante per le operazioni cargo.",  "Activa el combustible para operaciones de carga.",  "Включает расход топлива для грузовых операций.",  "Ativa o uso de combustível nas operações de carga.",  "Yük operasyonları için araç yakıtını etkinleştirir.");
            Add(translations,  ModTextKey.DetailCargoWeightPower,  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.",  "Reduce engine power as carried cargo weight increases.");
            Add(translations,  ModTextKey.DetailCruiseControl,  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.",  "Hold the current speed until you brake, disable it, or leave the vehicle.");
            Add(translations,  ModTextKey.DetailCargoDamage,  "Enable cargo loss and condition damage from collisions.",  "Active la perte de cargaison et les dégâts dus aux collisions.",  "Abilita perdita carico e danni da collisione.",  "Activa pérdida de carga y daños por colisiones.",  "Включает потерю груза и урон от столкновений.",  "Ativa perda de carga e dano por colisões.",  "Çarpışmalardan yük kaybı ve hasarını etkinleştirir.");
            Add(translations,  ModTextKey.DetailIndustryPriceMechanic,  "Require industry purchases and owner-cut payouts until bought.",  "Exige l'achat des industries et les commissions du propriétaire avant acquisition.",  "Richiede acquisti industria e percentuale proprietario fino all'acquisto.",  "Requiere comprar industrias y pagar comisión del dueño hasta adquirirlas.",  "Требует покупку отрасли и выплаты владельцу до выкупа.",  "Exige compra da indústria e repasse ao dono até a aquisição.",  "Satın alana kadar endüstri satın alma ve sahip payı ödemesi gerektirir.");
            Add(translations,  ModTextKey.DetailLicensingSystem,  "Require contractor permits before transporting cargo to or from industries.",  "Exige des permis avant de transporter du cargo vers ou depuis les industries.",  "Richiede permessi prima di trasportare carichi da o verso le industrie.",  "Requiere permisos antes de transportar carga hacia o desde industrias.",  "Требует разрешения перед перевозкой груза к отраслям и от них.",  "Exige licenças antes de transportar carga para ou das indústrias.",  "Endüstrilere yük taşımadan önce izin gerektirir.");
            Add(translations,  ModTextKey.DetailCorridorRestriction,  "Require unlocked corridors before cross-district logistics routes can operate.",  "Exige des corridors débloqués avant que les routes logistiques inter-districts puissent fonctionner.",  "Richiede corridoi sbloccati prima che le rotte logistiche tra distretti possano operare.",  "Requiere corredores desbloqueados antes de que puedan operar rutas logísticas entre distritos.",  "Требует разблокированные коридоры, прежде чем межрайонные логистические маршруты смогут работать.",  "Exige corredores desbloqueados antes que rotas logísticas entre distritos possam operar.",  "İlçeler arası lojistik rotaların çalışması için koridorların önce açılmış olmasını gerektirir.");
            Add(translations,  ModTextKey.DetailReputationSystem,  "Disable district reputation completely. Reputation gates, bonuses, penalties, and notices become inactive while off.",  "Désactive totalement la réputation des districts. Les blocages, bonus, pénalités et notifications deviennent inactifs.",  "Disattiva completamente la reputazione dei distretti. Blocchi, bonus, penalità e notifiche diventano inattivi.",  "Desactiva por completo la reputación de distrito. Bloqueos, bonificaciones, penalizaciones y avisos quedan inactivos.",  "Полностью отключает репутацию районов. Ограничения, бонусы, штрафы и уведомления становятся неактивны.",  "Desativa totalmente a reputação distrital. Restrições, bônus, penalidades e avisos ficam inativos.",  "Bölge itibarını tamamen kapatır. Kısıtlar, bonuslar, cezalar ve bildirimler devre dışı kalır.");
            Add(translations,  ModTextKey.DetailOfficeGarageLimit,  "Disable office garage capacity checks for purchases, storage, retrieval, spawning, and assignments.",  "Désactive les limites du garage pour achat, stockage, sortie, spawn et affectation.",  "Disattiva i controlli capienza garage per acquisti, deposito, ritiro, spawn e assegnazioni.",  "Desactiva los límites del garaje para compra, guardado, retirada, aparición y asignación.",  "Отключает лимит гаража офиса для покупки, хранения, выдачи, спавна и назначений.",  "Desativa os limites da garagem do escritório para compra, armazenamento, retirada, spawn e atribuição.",  "Satın alma, depolama, çağırma, doğma ve atamalar için ofis garaj kapasite kontrollerini kapatır.");
            Add(translations,  ModTextKey.DetailOfficeNpcLimitEnabled,  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.",  "Require Construction Site Cabin capacity before hiring additional NPCs.");
            Add(translations,  ModTextKey.DetailOfficeNpcLimitDisabled,  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.",  "Ignore Construction Site Cabin capacity when hiring NPCs.");
            Add(translations,  ModTextKey.DetailNpcRouteLimit,  "Set the maximum route slots per hired NPC. 0 disables Hiring NPC entirely.",  "Définit le nombre maximum de routes par PNJ recruté. 0 désactive le recrutement PNJ.",  "Imposta il numero massimo di rotte per ogni NPC assunto. 0 disattiva il reclutamento NPC.",  "Define el máximo de rutas por NPC contratado. 0 desactiva por completo los NPC contratados.",  "Задаёт максимум маршрутов на одного нанятого NPC. 0 полностью отключает найм NPC.",  "Define o máximo de rotas por NPC contratado. 0 desativa totalmente o Hiring NPC.",  "Kiralanan NPC başına en fazla rota sayısını ayarlar. 0, NPC işe alımını tamamen kapatır.");
            Add(translations,  ModTextKey.DetailCreateSave,  "Starts a fresh game as {0}.state.xml",  "Démarre une nouvelle partie sous {0}.state.xml",  "Avvia una nuova partita come {0}.state.xml",  "Inicia una nueva partida como {0}.state.xml",  "Запускает новую игру как {0}.state.xml",  "Inicia um novo jogo como {0}.state.xml",  "Yeni oyunu {0}.state.xml olarak başlatır");
            Add(translations,  ModTextKey.DetailLanguage,  "Apply language changes immediately across localized menu and status text.",  "Applique immédiatement la langue aux menus et statuts localisés.",  "Applica subito la lingua a menu e stati localizzati.",  "Aplica de inmediato el idioma a menús y estados localizados.",  "Сразу применяет язык к локализованным меню и статусам.",  "Aplica imediatamente o idioma a menus e textos de status localizados.",  "Yerelleştirilmiş menü ve durum yazılarına dili hemen uygular.");
            Add(translations,  ModTextKey.DetailLanguageEnglishFallback,  "English is active. Left or right switches to a translated language.",  "Le secours anglais est actif. Gauche ou droite passe à une langue traduite.",  "Il fallback inglese è attivo. Sinistra o destra passa a una lingua tradotta.",  "El inglés de respaldo está activo. Izquierda o derecha cambia a un idioma traducido.",  "Активен английский запасной язык. Влево или вправо переключает на перевод.",  "O fallback em inglês está ativo. Esquerda ou direita troca para um idioma traduzido.",  "İngilizce yedek dil etkin. Sağ veya sol ile çevrilmiş dillere geçin.");
            Add(translations,  ModTextKey.DetailSpeedUnitChanged,  "Speed units set to {0}.",  "Unités de vitesse réglées sur {0}.",  "Unità velocità impostate su {0}.",  "Unidades de velocidad establecidas en {0}.",  "Единицы скорости установлены: {0}.",  "Unidades de velocidade definidas para {0}.",  "Hız birimleri {0} olarak ayarlandı.");
            Add(translations,  ModTextKey.DetailColorblindMode,  "Apply a high-contrast mod UI palette tuned for the selected color vision mode.",  "Applique une palette à fort contraste adaptée au mode de vision sélectionné.",  "Applica una palette ad alto contrasto per la modalità visiva selezionata.",  "Aplica una paleta de alto contraste según la visión seleccionada.",  "Применяет высококонтрастную палитру интерфейса под выбранный режим зрения.",  "Aplica uma paleta de alto contraste ajustada ao modo de visão selecionado.",  "Seçilen renk görme moduna göre yüksek kontrastlı arayüz paleti uygular.");
            Add(translations,  ModTextKey.DetailOptions,  "Adjust language, speed units, and accessibility options.",  "Ajustez la langue et les options d'accessibilité.",  "Regola lingua e accessibilità.",  "Ajusta idioma y accesibilidad.",  "Настройте язык и доступность.",  "Ajuste idioma e acessibilidade.",  "Dil ve erişilebilirlik seçeneklerini ayarlayın.");
            Add(translations,  ModTextKey.DetailOptionsNavigate,  "Left or right cycles the available options.",  "Gauche ou droite fait défiler les options.",  "Sinistra o destra scorrono le opzioni.",  "Izquierda o derecha recorren las opciones.",  "Влево или вправо переключают варианты.",  "Esquerda ou direita percorrem as opções.",  "Sağ veya sol mevcut seçenekleri değiştirir.");
            Add(translations,  ModTextKey.DetailDifficultySettingsLockedStatus,  "Difficulty settings are sealed for this save. Create a new save to change them.",  "Les paramètres de difficulté sont scellés pour cette sauvegarde. Créez une nouvelle sauvegarde pour les modifier.",  "Le impostazioni difficoltà sono bloccate per questo salvataggio. Crea un nuovo salvataggio per cambiarle.",  "Los ajustes de dificultad están sellados para esta partida. Crea una nueva partida para cambiarlos.",  "Настройки сложности зафиксированы для этого сохранения. Создайте новое сохранение, чтобы изменить их.",  "As configurações de dificuldade estão seladas para este save. Crie um novo save para alterá-las.",  "Bu kayıt için zorluk ayarları mühürlü. Değiştirmek için yeni kayıt oluşturun.");
            Add(translations,  ModTextKey.DetailDifficultyTemplatesActions,  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.",  "Save the current full difficulty profile or load a saved one.");
            Add(translations,  ModTextKey.DetailDifficultyTemplatesActionsLocked,  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.",  "Save the current full difficulty profile here. Loading a saved template is unavailable while the active save is locked.");
            Add(translations,  ModTextKey.DetailDifficultyEnableAll,  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule on or enforced. Economy preset, wages, and NPC route limit stay as-is.");
            Add(translations,  ModTextKey.DetailDifficultyDisableAll,  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.",  "Turn every boolean difficulty rule off or bypassed. Economy preset, wages, and NPC route limit stay as-is.");
            Add(translations,  ModTextKey.DetailDifficultyBulkActionBlockedLocked,  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.",  "Locked by the active save. Bulk actions are unavailable here.");
            Add(translations,  ModTextKey.DetailDifficultySaveTemplate,  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.",  "Save the current full difficulty profile as a reusable global template.");
            Add(translations,  ModTextKey.DetailDifficultyLoadTemplate,  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.",  "Load a saved full difficulty profile into the current settings surface.");
            Add(translations,  ModTextKey.DetailDifficultyNoTemplatesFound,  "Save a template first to load it here.",  "Save a template first to load it here.",  "Save a template first to load it here.",  "Save a template first to load it here.",  "Save a template first to load it here.",  "Save a template first to load it here.",  "Save a template first to load it here.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateSaved,  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.",  "Saved difficulty template '{0}'.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateOverwritten,  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.",  "Overwrote difficulty template '{0}'.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateLoaded,  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.",  "Loaded difficulty template '{0}'.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateSaveCancelled,  "Difficulty template save cancelled.",  "Difficulty template save cancelled.",  "Difficulty template save cancelled.",  "Difficulty template save cancelled.",  "Difficulty template save cancelled.",  "Difficulty template save cancelled.",  "Difficulty template save cancelled.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateNameInvalid,  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.",  "Enter a valid difficulty template name.");
            Add(translations,  ModTextKey.DetailDifficultyTemplateBlockedLocked,  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.",  "Locked by the active save. Loading a difficulty template is unavailable here.");
            Add(translations,  ModTextKey.DetailPersistenceEnabled,  "Industry persistence enabled.",  "Persistance des industries activée.",  "Persistenza industrie attivata.",  "Persistencia industrial activada.",  "Сохранение отраслей включено.",  "Persistência da indústria ativada.",  "Endüstri kalıcılığı etkinleştirildi.");
            Add(translations,  ModTextKey.DetailPersistenceDisabled,  "Industry persistence disabled.",  "Persistance des industries désactivée.",  "Persistenza industrie disattivata.",  "Persistencia industrial desactivada.",  "Сохранение отраслей выключено.",  "Persistência da indústria desativada.",  "Endüstri kalıcılığı devre dışı.");
            Add(translations,  ModTextKey.DetailDebugUnavailable,  "Debug tools are only available while a debugger is attached.",  "Les outils debug ne sont disponibles qu'avec un débogueur attaché.",  "Gli strumenti debug sono disponibili solo con un debugger collegato.",  "Las herramientas de depuración solo están disponibles con un depurador conectado.",  "Инструменты отладки доступны только при подключенном отладчике.",  "Ferramentas de depuração só ficam disponíveis com um depurador anexado.",  "Hata ayıklama araçları yalnızca bir debugger bağlıyken kullanılabilir.");
            Add(translations,  ModTextKey.DetailLoadSavedState,  "Loaded saved industry state for {0} nodes.",  "État industriel chargé pour {0} nœuds.",  "Stato industria caricato per {0} nodi.",  "Estado industrial cargado para {0} nodos.",  "Загружено состояние отрасли для {0} узлов.",  "Estado da indústria carregado para {0} nós.",  "{0} düğüm için kayıtlı endüstri durumu yüklendi.");
            Add(translations,  ModTextKey.DetailLoadSavedSettings,  "Loaded saved game settings.",  "Paramètres de jeu chargés.",  "Impostazioni di gioco caricate.",  "Se cargaron los ajustes guardados.",  "Настройки игры загружены.",  "Configurações salvas carregadas.",  "Kayıtlı oyun ayarları yüklendi.");
            Add(translations,  ModTextKey.DetailNoSavedState,  "No saved industry state found yet.",  "Aucun état industriel sauvegardé pour le moment.",  "Nessuno stato industria salvato trovato.",  "Aún no se encontró estado industrial guardado.",  "Сохранённое состояние отрасли пока не найдено.",  "Nenhum estado da indústria salvo encontrado ainda.",  "Henüz kayıtlı endüstri durumu bulunamadı.");
            Add(translations,  ModTextKey.DetailNoSavePath,  "No save path is available.",  "Aucun chemin de sauvegarde disponible.",  "Nessun percorso di salvataggio disponibile.",  "No hay ruta de guardado disponible.",  "Путь сохранения недоступен.",  "Nenhum caminho de save disponível.",  "Kullanılabilir kayıt yolu yok.");
            Add(translations,  ModTextKey.DetailSaveCreationCancelled,  "Save creation cancelled.",  "Création de sauvegarde annulée.",  "Creazione salvataggio annullata.",  "Creación de partida cancelada.",  "Создание сохранения отменено.",  "Criação de save cancelada.",  "Kayıt oluşturma iptal edildi.");
            Add(translations,  ModTextKey.DetailSaveNameInvalid,  "Enter a valid save name.",  "Entrez un nom de sauvegarde valide.",  "Inserisci un nome salvataggio valido.",  "Introduce un nombre de partida válido.",  "Введите корректное имя сохранения.",  "Digite um nome de save válido.",  "Geçerli bir kayıt adı girin.");
            Add(translations,  ModTextKey.DetailSaveExists,  "A save with that name already exists.",  "Une sauvegarde avec ce nom existe déjà.",  "Esiste già un salvataggio con questo nome.",  "Ya existe una partida con ese nombre.",  "Сохранение с таким именем уже существует.",  "Já existe um save com esse nome.",  "Bu adda bir kayıt zaten var.");
            Add(translations,  ModTextKey.DetailNoPendingSaveName,  "No save name selected.",  "Aucun nom de sauvegarde sélectionné.",  "Nessun nome salvataggio selezionato.",  "No hay nombre de partida seleccionado.",  "Имя сохранения не выбрано.",  "Nenhum nome de save selecionado.",  "Seçili bir kayıt adı yok.");
            Add(translations,  ModTextKey.DetailSelectedSaveMissing,  "Selected save was not found.",  "La sauvegarde sélectionnée est introuvable.",  "Il salvataggio selezionato non è stato trovato.",  "No se encontró la partida seleccionada.",  "Выбранное сохранение не найдено.",  "O save selecionado não foi encontrado.",  "Seçilen kayıt bulunamadı.");
            Add(translations,  ModTextKey.DetailCreateOrLoadNamedSave,  "Create or load a named save first.",  "Créez ou chargez d'abord une sauvegarde nommée.",  "Crea o carica prima un salvataggio con nome.",  "Crea o carga primero una partida con nombre.",  "Сначала создайте или загрузите именованное сохранение.",  "Crie ou carregue um save nomeado primeiro.",  "Önce adlandırılmış bir kayıt oluşturun veya yükleyin.");
            Add(translations,  ModTextKey.DetailSaveCreated,  "Created save '{0}'.",  "Sauvegarde '{0}' créée.",  "Salvataggio '{0}' creato.",  "Partida '{0}' creada.",  "Сохранение '{0}' создано.",  "Save '{0}' criado.",  "'{0}' kaydı oluşturuldu.");
            Add(translations,  ModTextKey.DetailSaveLoaded,  "Loaded save '{0}'.",  "Sauvegarde '{0}' chargée.",  "Salvataggio '{0}' caricato.",  "Partida '{0}' cargada.",  "Сохранение '{0}' загружено.",  "Save '{0}' carregado.",  "'{0}' kaydı yüklendi.");
            Add(translations,  ModTextKey.DetailSaveDeleted,  "Deleted save '{0}'.",  "Sauvegarde '{0}' supprimée.",  "Salvataggio '{0}' eliminato.",  "Partida '{0}' eliminada.",  "Сохранение '{0}' удалено.",  "Save '{0}' excluído.",  "'{0}' kaydı silindi.");
            Add(translations,  ModTextKey.DetailSaveSaved,  "Saved '{0}'.",  "'{0}' sauvegardé.",  "'{0}' salvato.",  "'{0}' guardado.",  "'{0}' сохранено.",  "'{0}' salvo.",  "'{0}' kaydedildi.");
            Add(translations,  ModTextKey.DetailLoadedSuccessfully,  "Loaded successfully.",  "Chargé avec succès.",  "Caricato con successo.",  "Cargado correctamente.",  "Успешно загружено.",  "Carregado com sucesso.",  "Başarıyla yüklendi.");
            Add(translations,  ModTextKey.DetailMechanicsEnabled,  "Mod mechanics enabled.",  "Mécaniques du mod activées.",  "Meccaniche mod attivate.",  "Mecánicas del mod activadas.",  "Механики мода включены.",  "Mecânicas do mod ativadas.",  "Mod mekanikleri etkinleştirildi.");
            Add(translations,  ModTextKey.DetailMechanicsDisabled,  "Mod mechanics disabled.",  "Mécaniques du mod désactivées.",  "Meccaniche mod disattivate.",  "Mecánicas del mod desactivadas.",  "Механики мода отключены.",  "Mecânicas do mod desativadas.",  "Mod mekanikleri devre dışı.");
            Add(translations,  ModTextKey.DetailLanguageChanged,  "Language set to {0}.",  "Langue réglée sur {0}.",  "Lingua impostata su {0}.",  "Idioma configurado en {0}.",  "Язык изменён на {0}.",  "Idioma definido para {0}.",  "Dil {0} olarak ayarlandı.");
            Add(translations,  ModTextKey.DetailColorblindChanged,  "Colorblind mode set to {0}.",  "Mode daltonien réglé sur {0}.",  "Modalità daltonismo impostata su {0}.",  "Modo daltónico configurado en {0}.",  "Режим дальтонизма: {0}.",  "Modo daltônico definido para {0}.",  "Renk körü modu {0} olarak ayarlandı.");
            Add(translations,  ModTextKey.DetailEconomyPresetCasual,  "Industry price $200,000 | licence $8,000 | 180t input | 150t output | base 40 cyc/h. OmegaFactory x1.75, RecyclingCenter x3.00.",  "Prix industrie 200 000 $ | licence 8 000 $ | entrée 180t | sortie 150t | base 40 cyc/h. OmegaFactory x1,75, RecyclingCenter x3,00.",  "Prezzo industria $200.000 | licenza $8.000 | input 180t | output 150t | base 40 cic/h. OmegaFactory x1,75, RecyclingCenter x3,00.",  "Precio industria $200,000 | licencia $8,000 | entrada 180t | salida 150t | base 40 cic/h. OmegaFactory x1.75, RecyclingCenter x3.00.",  "Цена отрасли $200,000 | лицензия $8,000 | вход 180т | выход 150т | база 40 цик/ч. OmegaFactory x1.75, RecyclingCenter x3.00.",  "Preço da indústria $200.000 | licença $8.000 | entrada 180t | saída 150t | base 40 cyc/h. OmegaFactory x1,75, RecyclingCenter x3,00.",  "Endüstri fiyatı $200.000 | lisans $8.000 | 180t girdi | 150t çıktı | taban 40 döngü/s. OmegaFactory x1,75, RecyclingCenter x3,00.");
            Add(translations,  ModTextKey.DetailEconomyPresetStandard,  "Industry price $450,000 | licence $13,000 | 120t input | 100t output | base 32 cyc/h. OmegaFactory x1.50, RecyclingCenter x2.50.",  "Prix industrie 450 000 $ | licence 13 000 $ | entrée 120t | sortie 100t | base 32 cyc/h. OmegaFactory x1,50, RecyclingCenter x2,50.",  "Prezzo industria $450.000 | licenza $13.000 | input 120t | output 100t | base 32 cic/h. OmegaFactory x1,50, RecyclingCenter x2,50.",  "Precio industria $450,000 | licencia $13,000 | entrada 120t | salida 100t | base 32 cic/h. OmegaFactory x1.50, RecyclingCenter x2.50.",  "Цена отрасли $450,000 | лицензия $13,000 | вход 120т | выход 100т | база 32 цик/ч. OmegaFactory x1.50, RecyclingCenter x2.50.",  "Preço da indústria $450.000 | licença $13.000 | entrada 120t | saída 100t | base 32 cyc/h. OmegaFactory x1,50, RecyclingCenter x2,50.",  "Endüstri fiyatı $450.000 | lisans $13.000 | 120t girdi | 100t çıktı | taban 32 döngü/s. OmegaFactory x1,50, RecyclingCenter x2,50.");
            Add(translations,  ModTextKey.DetailEconomyPresetHardcore,  "Industry price $800,000 | licence $18,000 | 80t input | 70t output | base 24 cyc/h. OmegaFactory x1.25, RecyclingCenter x2.00.",  "Prix industrie 800 000 $ | licence 18 000 $ | entrée 80t | sortie 70t | base 24 cyc/h. OmegaFactory x1,25, RecyclingCenter x2,00.",  "Prezzo industria $800.000 | licenza $18.000 | input 80t | output 70t | base 24 cic/h. OmegaFactory x1,25, RecyclingCenter x2,00.",  "Precio industria $800,000 | licencia $18,000 | entrada 80t | salida 70t | base 24 cic/h. OmegaFactory x1.25, RecyclingCenter x2.00.",  "Цена отрасли $800,000 | лицензия $18,000 | вход 80т | выход 70т | база 24 цик/ч. OmegaFactory x1.25, RecyclingCenter x2.00.",  "Preço da indústria $800.000 | licença $18.000 | entrada 80t | saída 70t | base 24 cyc/h. OmegaFactory x1,25, RecyclingCenter x2,00.",  "Endüstri fiyatı $800.000 | lisans $18.000 | 80t girdi | 70t çıktı | taban 24 döngü/s. OmegaFactory x1,25, RecyclingCenter x2,00.");
            Add(translations,  ModTextKey.DetailEconomyPresetImpossible,  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.",  "Industry price $1,400,000 | licence $25,000 | 55t input | 50t output | base 17 cyc/h. OmegaFactory x1.10, RecyclingCenter x1.75.");
            Add(translations,  ModTextKey.DetailNpcWeeklyPayroll,  "Weekly NPC payroll only. Rookie {0} | Pro {1} | Veteran {2}.",  "Paie hebdo PNJ seulement. Débutant {0} | Pro {1} | Vétéran {2}.",  "Solo paga settimanale NPC. Rookie {0} | Pro {1} | Veterano {2}.",  "Solo nómina semanal NPC. Novato {0} | Pro {1} | Veterano {2}.",  "Только недельная зарплата NPC. Новичок {0} | Профи {1} | Ветеран {2}.",  "Apenas folha semanal dos NPCs. Novato {0} | Pro {1} | Veterano {2}.",  "Yalnızca haftalık NPC maaşı. Çaylak {0} | Pro {1} | Kıdemli {2}.");

            Add(translations,  ModTextKey.LemonToggleHint,  "Press Enter to toggle.",  "Appuyez sur Entrée pour basculer.",  "Premi Invio per attivare.",  "Pulsa Intro para alternar.",  "Нажмите Enter для переключения.",  "Pressione Enter para alternar.",  "Açıp kapatmak için Enter'a basın.");
            Add(translations,  ModTextKey.LemonAdjustHint,  "Left/Right to adjust.",  "Gauche/Droite pour ajuster.",  "Sinistra/Destra per regolare.",  "Izquierda/Derecha para ajustar.",  "Влево/вправо для изменения.",  "Esquerda/Direita para ajustar.",  "Ayarlamak için Sol/Sağ.");
            Add(translations,  ModTextKey.LemonCycleHint,  "Left/Right to cycle options.",  "Gauche/Droite pour faire défiler les options.",  "Sinistra/Destra per scorrere le opzioni.",  "Izquierda/Derecha para cambiar opciones.",  "Влево/вправо для выбора вариантов.",  "Esquerda/Direita para percorrer opções.",  "Seçenekler arasında geçmek için Sol/Sağ.");
            Add(translations,  ModTextKey.LemonUseHint,  "Press Enter to use this option.",  "Appuyez sur Entrée pour utiliser cette option.",  "Premi Invio per usare questa opzione.",  "Pulsa Intro para usar esta opción.",  "Нажмите Enter, чтобы использовать этот пункт.",  "Pressione Enter para usar esta opção.",  "Bu seçeneği kullanmak için Enter'a basın.");

            Add(translations,  ModTextKey.ValueDefaultAutosave,  "Default autosave",  "Sauvegarde auto par défaut",  "Autosalvataggio predefinito",  "Autoguardado predeterminado",  "Автосохранение по умолчанию",  "Autossalvamento padrão",  "Varsayılan otomatik kayıt");
            Add(translations,  ModTextKey.ValueDifficultyCasual,  "Casual",  "Détendu",  "Casual",  "Casual",  "Лёгкий",  "Casual",  "Rahat");
            Add(translations,  ModTextKey.ValueDifficultyStandard,  "Standard",  "Standard",  "Standard",  "Estándar",  "Стандарт",  "Padrão",  "Standart");
            Add(translations,  ModTextKey.ValueDifficultyHardcore,  "Hardcore",  "Hardcore",  "Hardcore",  "Hardcore",  "Хардкор",  "Hardcore",  "Zorlayıcı");
            Add(translations,  ModTextKey.ValueDifficultyImpossible,  "Impossible",  "Impossible",  "Impossibile",  "Imposible",  "Невозможно",  "Impossível",  "İmkansız");

            Add(translations,  ModTextKey.ValueLanguageEnglishFallback,  "English",  "Anglais",  "Fallback inglese",  "Inglés de respaldo",  "Резервный английский",  "Inglês de fallback",  "İngilizce yedek");
            Add(translations,  ModTextKey.ValueLanguageFrench,  "French",  "Français",  "Francese",  "Francés",  "Французский",  "Francês",  "Fransızca");
            Add(translations,  ModTextKey.ValueLanguageItalian,  "Italian",  "Italien",  "Italiano",  "Italiano",  "Итальянский",  "Italiano",  "İtalyanca");
            Add(translations,  ModTextKey.ValueLanguageSpanish,  "Spanish",  "Espagnol",  "Spagnolo",  "Español",  "Испанский",  "Espanhol",  "İspanyolca");
            Add(translations,  ModTextKey.ValueLanguageRussian,  "Russian",  "Russe",  "Russo",  "Ruso",  "Русский",  "Russo",  "Rusça");
            Add(translations,  ModTextKey.ValueLanguagePortuguese,  "Portuguese",  "Portugais",  "Portoghese",  "Portugués",  "Португальский",  "Português",  "Portekizce");
            Add(translations,  ModTextKey.ValueLanguageTurkish,  "Turkish",  "Turc",  "Turco",  "Turco",  "Турецкий",  "Turco",  "Türkçe");

            Add(translations,  ModTextKey.ValueUnitImperial,  "Imperial",  "Impérial",  "Imperiale",  "Imperial",  "Имперские",  "Imperial",  "İmperyal");
            Add(translations,  ModTextKey.ValueUnitMetric,  "Metric",  "Métrique",  "Metrico",  "Métrico",  "Метрические",  "Métrico",  "Metrik");

            Add(translations,  ModTextKey.ValueColorblindOff,  "Off",  "Désactivé",  "Disattivato",  "Desactivado",  "Выкл.",  "Desligado",  "Kapalı");
            Add(translations,  ModTextKey.ValueColorblindDeuteranopia,  "Deuteranopia",  "Deutéranopie",  "Deuteranopia",  "Deuteranopía",  "Дейтеранопия",  "Deuteranopia",  "Döteranopi");
            Add(translations,  ModTextKey.ValueColorblindProtanopia,  "Protanopia",  "Protanopie",  "Protanopia",  "Protanopía",  "Протанопия",  "Protanopia",  "Protanopi");
            Add(translations,  ModTextKey.ValueColorblindTritanopia,  "Tritanopia",  "Tritanopie",  "Tritanopia",  "Tritanopía",  "Тританопия",  "Tritanopia",  "Tritanopi");

            TabletLocalizationCatalog.AddTranslations(translations);

            return translations;
        }

        private static void Add(
            IDictionary<string, Dictionary<ModLanguage, string>> translations,
            string key,
            string english,
            string french,
            string italian,
            string spanish,
            string russian,
            string portuguese,
            string turkish)
        {
            translations[key] = new Dictionary<ModLanguage, string>
            {
                { ModLanguage.English, english },
                { ModLanguage.French, french },
                { ModLanguage.Italian, italian },
                { ModLanguage.Spanish, spanish },
                { ModLanguage.Russian, russian },
                { ModLanguage.Portuguese, portuguese },
                { ModLanguage.Turkish, turkish },
            };
        }
    }

    internal static class ModLocalization
    {
        private static readonly ModLocalizationService ServiceInstance = new ModLocalizationService();

        public static ModLocalizationService Service
        {
            get { return ServiceInstance; }
        }
    }
}
