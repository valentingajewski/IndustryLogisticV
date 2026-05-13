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
        Japanese = 5,
        Chinese = 6,
        Hindi = 7,
        Portuguese = 8,
        Turkish = 9,
    }

    internal static class ModTextKey
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
        public const string RowNpcRouteLimit = "row.npcRouteLimit";
        public const string RowEconomyPreset = "row.economyPreset";
        public const string RowNpcWeeklyWages = "row.npcWeeklyWages";
        public const string RowStartingBalance = "row.startingBalance";
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
        public const string DetailNpcRouteLimit = "detail.npcRouteLimit";
        public const string DetailCreateSave = "detail.createSave";
        public const string DetailLanguage = "detail.language";
        public const string DetailLanguageEnglishFallback = "detail.language.englishFallback";
        public const string DetailSpeedUnitChanged = "detail.speedUnitChanged";
        public const string DetailColorblindMode = "detail.colorblindMode";
        public const string DetailOptions = "detail.options";
        public const string DetailOptionsNavigate = "detail.options.navigate";
        public const string DetailDifficultySettingsLockedStatus = "detail.difficulty.lockedStatus";
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
        public const string DetailNpcWeeklyPayroll = "detail.npcWeeklyPayroll";

        public const string LemonToggleHint = "lemon.toggleHint";
        public const string LemonAdjustHint = "lemon.adjustHint";
        public const string LemonCycleHint = "lemon.cycleHint";
        public const string LemonUseHint = "lemon.useHint";

        public const string ValueDefaultAutosave = "value.defaultAutosave";
        public const string ValueDifficultyCasual = "value.difficulty.casual";
        public const string ValueDifficultyStandard = "value.difficulty.standard";
        public const string ValueDifficultyHardcore = "value.difficulty.hardcore";

        public const string ValueLanguageEnglishFallback = "value.language.englishFallback";
        public const string ValueLanguageFrench = "value.language.french";
        public const string ValueLanguageItalian = "value.language.italian";
        public const string ValueLanguageSpanish = "value.language.spanish";
        public const string ValueLanguageRussian = "value.language.russian";
        public const string ValueLanguageJapanese = "value.language.japanese";
        public const string ValueLanguageChinese = "value.language.chinese";
        public const string ValueLanguageHindi = "value.language.hindi";
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

            Add(translations, ModTextKey.CommonOn, "On", "Activé", "Attivo", "Activado", "Вкл.", "オン", "开", "चालू", "Ligado", "Açık");
            Add(translations, ModTextKey.CommonOff, "Off", "Désactivé", "Disattivato", "Desactivado", "Выкл.", "オフ", "关", "बंद", "Desligado", "Kapalı");
            Add(translations, ModTextKey.CommonBack, "Back", "Retour", "Indietro", "Atrás", "Назад", "戻る", "返回", "वापस", "Voltar", "Geri");
            Add(translations, ModTextKey.CommonClose, "Close", "Fermer", "Chiudi", "Cerrar", "Закрыть", "閉じる", "关闭", "बंद करें", "Fechar", "Kapat");
            Add(translations, ModTextKey.CommonCreate, "Create", "Créer", "Crea", "Crear", "Создать", "作成", "创建", "बनाएं", "Criar", "Oluştur");
            Add(translations, ModTextKey.CommonDelete, "Delete", "Supprimer", "Elimina", "Eliminar", "Удалить", "削除", "删除", "हटाएं", "Excluir", "Sil");
            Add(translations, ModTextKey.CommonLoad, "Load", "Charger", "Carica", "Cargar", "Загрузить", "読み込む", "加载", "लोड करें", "Carregar", "Yükle");
            Add(translations, ModTextKey.CommonSave, "Save", "Sauvegarder", "Salva", "Guardar", "Сохранить", "保存", "保存", "सहेजें", "Salvar", "Kaydet");

            Add(translations, ModTextKey.MenuGameModControlTitle, "Game Mod Control", "Contrôle du mod", "Controllo mod", "Control del mod", "Управление модом", "MOD管理", "模组控制", "मॉड नियंत्रण", "Controle do mod", "Mod kontrolü");
            Add(translations, ModTextKey.MenuGameModControlSubtitle, "Activate mechanics and configure gameplay", "Activer les mécaniques et configurer le gameplay", "Attiva le meccaniche e configura il gameplay", "Activa las mecánicas y configura la partida", "Включайте механики и настраивайте игровой процесс", "ゲームプレイ設定と機能切替", "启用机制并配置玩法", "मेकैनिक्स चालू करें और गेमप्ले सेट करें", "Ative mecânicas e configure a jogabilidade", "Mekanikleri açın ve oynanışı ayarlayın");
            Add(translations, ModTextKey.MenuSavingOptionsTitle, "Saving Options", "Options de sauvegarde", "Opzioni di salvataggio", "Opciones de guardado", "Параметры сохранения", "保存オプション", "保存选项", "सेव विकल्प", "Opções de salvamento", "Kayıt seçenekleri");
            Add(translations, ModTextKey.MenuSavingOptionsSubtitle, "Active save: {0}", "Sauvegarde active : {0}", "Salvataggio attivo: {0}", "Partida activa: {0}", "Активное сохранение: {0}", "アクティブセーブ: {0}", "当前存档：{0}", "सक्रिय सेव: {0}", "Save ativo: {0}", "Etkin kayıt: {0}");
            Add(translations, ModTextKey.MenuDifficultyTitle, "Difficulty Settings", "Paramètres de difficulté", "Impostazioni difficoltà", "Ajustes de dificultad", "Настройки сложности", "難易度設定", "难度设置", "कठिनाई सेटिंग्स", "Configurações de dificuldade", "Zorluk ayarları");
            Add(translations, ModTextKey.MenuDifficultySubtitle, "Enable or disable challenge options", "Activer ou désactiver les options de défi", "Attiva o disattiva le opzioni di sfida", "Activa o desactiva opciones de desafío", "Включайте и отключайте параметры испытания", "チャレンジ項目を切り替え", "启用或禁用挑战选项", "चैलेंज विकल्प चालू या बंद करें", "Ative ou desative opções de desafio", "Meydan okuma seçeneklerini açıp kapatın");
            Add(translations, ModTextKey.MenuDifficultyLockedSubtitle, "Locked by the active save. Values are read-only.", "Verrouillé par la sauvegarde active. Valeurs en lecture seule.", "Bloccato dal salvataggio attivo. Valori in sola lettura.", "Bloqueado por la partida activa. Valores de solo lectura.", "Заблокировано активным сохранением. Значения только для чтения.", "現在のセーブで固定。読み取り専用です。", "由当前存档锁定，仅可查看。", "सक्रिय सेव द्वारा लॉक। केवल पढ़ने योग्य।", "Bloqueado pelo save ativo. Valores somente leitura.", "Etkin kayıt tarafından kilitlendi. Değerler salt okunur.");
            Add(translations, ModTextKey.MenuNewSaveSetupSubtitle, "Configure '{0}' before starting", "Configurez '{0}' avant de commencer", "Configura '{0}' prima di iniziare", "Configura '{0}' antes de empezar", "Настройте '{0}' перед стартом", "開始前に '{0}' を設定", "开始前配置“{0}”", "शुरू करने से पहले '{0}' सेट करें", "Configure '{0}' antes de começar", "Başlamadan önce '{0}' yapılandırın");
            Add(translations, ModTextKey.MenuSaveSlotsLoadTitle, "Load save", "Charger une sauvegarde", "Carica salvataggio", "Cargar partida", "Загрузить сохранение", "セーブを読み込む", "加载存档", "सेव लोड करें", "Carregar save", "Kaydı yükle");
            Add(translations, ModTextKey.MenuSaveSlotsDeleteTitle, "Delete save", "Supprimer une sauvegarde", "Elimina salvataggio", "Eliminar partida", "Удалить сохранение", "セーブを削除", "删除存档", "सेव हटाएं", "Excluir save", "Kaydı sil");
            Add(translations, ModTextKey.MenuSaveSlotsSingle, "1 savegame found", "1 sauvegarde trouvée", "1 salvataggio trovato", "1 partida encontrada", "Найдено 1 сохранение", "セーブ1件", "找到 1 个存档", "1 सेव मिला", "1 save encontrado", "1 kayıt bulundu");
            Add(translations, ModTextKey.MenuSaveSlotsMany, "{0} savegames found", "{0} sauvegardes trouvées", "{0} salvataggi trovati", "{0} partidas encontradas", "Найдено сохранений: {0}", "セーブ{0}件", "找到 {0} 个存档", "{0} सेव मिले", "{0} saves encontrados", "{0} kayıt bulundu");
            Add(translations, ModTextKey.MenuOptionsTitle, "Options", "Options", "Opzioni", "Opciones", "Параметры", "オプション", "选项", "विकल्प", "Opções", "Seçenekler");
            Add(translations, ModTextKey.MenuOptionsSubtitle, "Language, units, and accessibility settings", "Langue et accessibilité", "Lingua e accessibilità", "Idioma y accesibilidad", "Язык и специальные возможности", "言語とアクセシビリティ", "语言和辅助功能", "भाषा और पहुँच", "Idioma e acessibilidade", "Dil ve erişilebilirlik ayarları");

            Add(translations, ModTextKey.RowSavingOptions, "Saving Options", "Options de sauvegarde", "Opzioni di salvataggio", "Opciones de guardado", "Параметры сохранения", "保存オプション", "保存选项", "सेव विकल्प", "Opções de salvamento", "Kayıt seçenekleri");
            Add(translations, ModTextKey.RowDifficultySettings, "Difficulty settings", "Paramètres de difficulté", "Impostazioni difficoltà", "Ajustes de dificultad", "Настройки сложности", "難易度設定", "难度设置", "कठिनाई सेटिंग्स", "Configurações de dificuldade", "Zorluk ayarları");
            Add(translations, ModTextKey.RowOptions, "Options", "Options", "Opzioni", "Opciones", "Параметры", "オプション", "选项", "विकल्प", "Opções", "Seçenekler");
            Add(translations, ModTextKey.RowCreateNewSave, "Create new save", "Créer une nouvelle sauvegarde", "Crea nuovo salvataggio", "Crear nueva partida", "Создать новое сохранение", "新しいセーブを作成", "创建新存档", "नई सेव बनाएं", "Criar novo save", "Yeni kayıt oluştur");
            Add(translations, ModTextKey.RowLoadSave, "Load save", "Charger une sauvegarde", "Carica salvataggio", "Cargar partida", "Загрузить сохранение", "セーブを読み込む", "加载存档", "सेव लोड करें", "Carregar save", "Kaydı yükle");
            Add(translations, ModTextKey.RowDeleteSave, "Delete save", "Supprimer une sauvegarde", "Elimina salvataggio", "Eliminar partida", "Удалить сохранение", "セーブを削除", "删除存档", "सेव हटाएं", "Excluir save", "Kaydı sil");
            Add(translations, ModTextKey.RowSaveGame, "Save game", "Sauvegarder la partie", "Salva partita", "Guardar partida", "Сохранить игру", "ゲームを保存", "保存游戏", "गेम सेव करें", "Salvar jogo", "Oyunu kaydet");
            Add(translations, ModTextKey.RowCreateSave, "Create save", "Créer la sauvegarde", "Crea salvataggio", "Crear partida", "Создать сохранение", "セーブを作成", "创建存档", "सेव बनाएं", "Criar save", "Kaydı oluştur");
            Add(translations, ModTextKey.RowNoSavesFound, "No saves found", "Aucune sauvegarde trouvée", "Nessun salvataggio trovato", "No se encontraron partidas", "Сохранения не найдены", "セーブが見つかりません", "未找到存档", "कोई सेव नहीं मिला", "Nenhum save encontrado", "Kayıt bulunamadı");
            Add(translations, ModTextKey.RowLanguage, "Language: < {0} >", "Langue : < {0} >", "Lingua: < {0} >", "Idioma: < {0} >", "Язык: < {0} >", "言語: < {0} >", "语言：< {0} >", "भाषा: < {0} >", "Idioma: < {0} >", "Dil: < {0} >");
            Add(translations, ModTextKey.RowSpeedUnit, "Speed units: < {0} >", "Unités de vitesse : < {0} >", "Unità velocità: < {0} >", "Unidades de velocidad: < {0} >", "Единицы скорости: < {0} >", "速度単位: < {0} >", "速度单位：< {0} >", "स्पीड यूनिट: < {0} >", "Unidades de velocidade: < {0} >", "Hız birimleri: < {0} >");
            Add(translations, ModTextKey.RowColorblindMode, "Colorblind mode: < {0} >", "Mode daltonien : < {0} >", "Modalità daltonismo: < {0} >", "Modo daltónico: < {0} >", "Режим дальтонизма: < {0} >", "色覚サポート: < {0} >", "色盲模式：< {0} >", "कलरब्लाइंड मोड: < {0} >", "Modo daltônico: < {0} >", "Renk körü modu: < {0} >");
            Add(translations, ModTextKey.RowVehicleFuel, "Vehicle fuel", "Carburant véhicule", "Carburante veicolo", "Combustible del vehículo", "Топливо транспорта", "車両燃料", "车辆燃料", "वाहन ईंधन", "Combustível do veículo", "Araç yakıtı");
            Add(translations, ModTextKey.RowCargoWeightPower, "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power", "Cargo weight power");
            Add(translations, ModTextKey.RowCruiseControl, "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}", "Cruise control: {0}");
            Add(translations, ModTextKey.RowCargoDamage, "Cargo damage", "Dégâts de cargaison", "Danni al carico", "Daño de la carga", "Повреждение груза", "貨物ダメージ", "货物损伤", "कार्गो क्षति", "Dano da carga", "Yük hasarı");
            Add(translations, ModTextKey.RowIndustryPriceMechanic, "Industry price mechanic", "Mécanique de prix d'industrie", "Meccanica prezzo industria", "Mecánica de precio industrial", "Механика стоимости отрасли", "産業価格メカニクス", "产业价格机制", "इंडस्ट्री मूल्य प्रणाली", "Mecânica de preço da indústria", "Endüstri fiyat mekaniği");
            Add(translations, ModTextKey.RowLicensingSystem, "Licensing system", "Système de licence", "Sistema di licenze", "Sistema de licencias", "Система лицензий", "ライセンス制度", "许可证系统", "लाइसेंस सिस्टम", "Sistema de licenças", "Lisans sistemi");
            Add(translations, ModTextKey.RowCorridorRestriction, "Corridor restriction", "Restriction de corridor", "Restrizione corridoi", "Restricción de corredores", "Ограничение коридоров", "回廊制限", "走廊限制", "कॉरिडोर प्रतिबंध", "Restrição de corredores", "Koridor kısıtlaması");
            Add(translations, ModTextKey.RowReputationSystem, "Reputation system", "Système de réputation", "Sistema reputazione", "Sistema de reputación", "Система репутации", "評判システム", "声望系统", "रिप्यूटेशन सिस्टम", "Sistema de reputação", "İtibar sistemi");
            Add(translations, ModTextKey.RowOfficeGarageLimit, "Office garage limit", "Limite garage bureau", "Limite garage ufficio", "Límite de garaje de oficina", "Лимит гаража офиса", "オフィスガレージ上限", "办公室车库上限", "ऑफिस गैरेज सीमा", "Limite da garagem do escritório", "Ofis garaj sınırı");
            Add(translations, ModTextKey.RowNpcRouteLimit, "NPC route limit: < {0} >", "Limite de routes PNJ : < {0} >", "Limite rotte NPC: < {0} >", "Límite de rutas NPC: < {0} >", "Лимит маршрутов NPC: < {0} >", "NPC ルート上限: < {0} >", "NPC 路线限制：< {0} >", "NPC रूट सीमा: < {0} >", "Limite de rotas NPC: < {0} >", "NPC rota sınırı: < {0} >");
            Add(translations, ModTextKey.RowEconomyPreset, "Economy preset: < {0} >", "Préréglage économie : < {0} >", "Preset economia: < {0} >", "Preajuste económico: < {0} >", "Экономика: < {0} >", "経済プリセット: < {0} >", "经济预设：< {0} >", "इकोनॉमी प्रीसेट: < {0} >", "Predefinição econômica: < {0} >", "Ekonomi ön ayarı: < {0} >");
            Add(translations, ModTextKey.RowNpcWeeklyWages, "NPC weekly wages: < {0} >", "Salaires hebdo PNJ : < {0} >", "Salari settimanali NPC: < {0} >", "Sueldos semanales NPC: < {0} >", "Недельная зарплата NPC: < {0} >", "NPC週給: < {0} >", "NPC 周薪：< {0} >", "NPC साप्ताहिक वेतन: < {0} >", "Salários semanais dos NPCs: < {0} >", "NPC haftalık ücretleri: < {0} >");
            Add(translations, ModTextKey.RowStartingBalance, "Starting balance: {0}", "Solde initial : {0}", "Saldo iniziale: {0}", "Saldo inicial: {0}", "Стартовый баланс: {0}", "開始資金: {0}", "初始资金：{0}", "शुरुआती बैलेंस: {0}", "Saldo inicial: {0}", "Başlangıç bakiyesi: {0}");
            Add(translations, ModTextKey.RowActivate, "Activate: {0}", "Activer : {0}", "Attiva: {0}", "Activar: {0}", "Активировать: {0}", "有効化: {0}", "启用：{0}", "सक्रिय करें: {0}", "Ativar: {0}", "Etkinleştir: {0}");

            Add(translations, ModTextKey.DetailSavingOptions, "Create, load, delete, or save named games. Active: {0}.", "Créer, charger, supprimer ou sauvegarder des parties nommées. Active : {0}.", "Crea, carica, elimina o salva partite nominate. Attiva: {0}.", "Crea, carga, elimina o guarda partidas con nombre. Activa: {0}.", "Создавайте, загружайте, удаляйте и сохраняйте именованные игры. Активно: {0}.", "名前付きセーブの作成・読込・削除・保存。現在: {0}。", "创建、加载、删除或保存命名存档。当前：{0}。", "नामित सेव बनाएं, लोड करें, हटाएं या सेव करें। सक्रिय: {0}।", "Crie, carregue, exclua ou salve jogos nomeados. Ativo: {0}.", "Adlandırılmış kayıtlar oluşturun, yükleyin, silin veya kaydedin. Etkin: {0}.");
            Add(translations, ModTextKey.DetailDifficultyUnlocked, "Change the economy preset, vehicle fuel, cargo damage, industry pricing, licensing, and NPC wages. Economy: {0} | wages: {1}.", "Modifiez l'économie, le carburant, les dégâts cargo, le prix des industries, les licences et les salaires PNJ. Économie : {0} | salaires : {1}.", "Modifica preset economia, carburante, danni al carico, prezzi industria, licenze e salari NPC. Economia: {0} | salari: {1}.", "Cambia economía, combustible, daño de carga, precios industriales, licencias y salarios NPC. Economía: {0} | salarios: {1}.", "Меняйте экономику, топливо, урон грузу, цены отраслей, лицензии и зарплаты NPC. Экономика: {0} | зарплаты: {1}.", "経済、燃料、貨物ダメージ、産業価格、ライセンス、NPC賃金を変更。経済: {0} | 賃金: {1}。", "调整经济、燃料、货损、产业价格、许可和 NPC 工资。经济：{0} | 工资：{1}。", "इकोनॉमी, ईंधन, कार्गो डैमेज, इंडस्ट्री कीमत, लाइसेंस और NPC वेतन बदलें। इकोनॉमी: {0} | वेतन: {1}।", "Altere economia, combustível, dano de carga, preços de indústria, licenças e salários de NPCs. Economia: {0} | salários: {1}.", "Ekonomi, yakıt, yük hasarı, endüstri fiyatı, lisans ve NPC ücretlerini değiştirin. Ekonomi: {0} | ücretler: {1}.");
            Add(translations, ModTextKey.DetailDifficultyLocked, "Locked by the active save. Economy: {0} | NPC wages: {1}. Create a new save to change these settings.", "Verrouillé par la sauvegarde active. Économie : {0} | salaires PNJ : {1}. Créez une nouvelle sauvegarde pour changer ces paramètres.", "Bloccato dal salvataggio attivo. Economia: {0} | salari NPC: {1}. Crea un nuovo salvataggio per cambiare queste impostazioni.", "Bloqueado por la partida activa. Economía: {0} | salarios NPC: {1}. Crea una nueva partida para cambiar estos ajustes.", "Заблокировано активным сохранением. Экономика: {0} | зарплаты NPC: {1}. Создайте новое сохранение, чтобы изменить параметры.", "現在のセーブで固定。経済: {0} | NPC賃金: {1}。変更するには新しいセーブを作成してください。", "由当前存档锁定。经济：{0} | NPC 工资：{1}。要更改请创建新存档。", "सक्रिय सेव द्वारा लॉक। इकोनॉमी: {0} | NPC वेतन: {1}। बदलने के लिए नई सेव बनाएं।", "Bloqueado pelo save ativo. Economia: {0} | salários de NPCs: {1}. Crie um novo save para alterar essas configurações.", "Etkin kayıt tarafından kilitlendi. Ekonomi: {0} | NPC ücretleri: {1}. Bu ayarları değiştirmek için yeni kayıt oluşturun.");
            Add(translations, ModTextKey.DetailCreateNewSave, "Creates {0}.state.ini in {1}", "Crée {0}.state.ini dans {1}", "Crea {0}.state.ini in {1}", "Crea {0}.state.ini en {1}", "Создаёт {0}.state.ini в {1}", "{1} に {0}.state.ini を作成", "在 {1} 中创建 {0}.state.ini", "{1} में {0}.state.ini बनाता है", "Cria {0}.state.ini em {1}", "{1} içinde {0}.state.ini oluşturur");
            Add(translations, ModTextKey.DetailLoadSave, "Choose from all created savegames.", "Choisissez parmi toutes les sauvegardes créées.", "Scegli tra tutti i salvataggi creati.", "Elige entre todas las partidas creadas.", "Выберите одно из созданных сохранений.", "作成済みセーブから選択。", "从已创建的存档中选择。", "बने हुए सेवों में से चुनें।", "Escolha entre todos os saves criados.", "Oluşturulan tüm kayıtlardan seçin.");
            Add(translations, ModTextKey.DetailDeleteSave, "Delete one of your created savegames.", "Supprimez une de vos sauvegardes.", "Elimina uno dei tuoi salvataggi.", "Elimina una de tus partidas creadas.", "Удалите одно из созданных сохранений.", "作成済みセーブを削除。", "删除你创建的一个存档。", "अपने बनाए सेवों में से एक हटाएं।", "Exclua um dos seus saves criados.", "Oluşturduğunuz kayıtlardan birini silin.");
            Add(translations, ModTextKey.DetailSaveGame, "Writes current progress to {0}.state.ini.", "Écrit la progression actuelle dans {0}.state.ini.", "Scrive i progressi correnti in {0}.state.ini.", "Escribe el progreso actual en {0}.state.ini.", "Записывает текущий прогресс в {0}.state.ini.", "現在の進行状況を {0}.state.ini に保存。", "将当前进度写入 {0}.state.ini。", "वर्तमान प्रगति {0}.state.ini में लिखता है।", "Grava o progresso atual em {0}.state.ini.", "Geçerli ilerlemeyi {0}.state.ini dosyasına yazar.");
            Add(translations, ModTextKey.DetailNeedNamedSave, "Create or load a named save first.", "Créez ou chargez d'abord une sauvegarde nommée.", "Crea o carica prima un salvataggio con nome.", "Crea o carga primero una partida con nombre.", "Сначала создайте или загрузите именованное сохранение.", "先に名前付きセーブを作成または読込。", "请先创建或加载命名存档。", "पहले नामित सेव बनाएं या लोड करें।", "Crie ou carregue um save nomeado primeiro.", "Önce adlandırılmış bir kayıt oluşturun veya yükleyin.");
            Add(translations, ModTextKey.DetailNoSavesFound, "Create a save in {0} first.", "Créez d'abord une sauvegarde dans {0}.", "Crea prima un salvataggio in {0}.", "Primero crea una partida en {0}.", "Сначала создайте сохранение в {0}.", "先に {0} にセーブを作成。", "请先在 {0} 中创建存档。", "पहले {0} में सेव बनाएं।", "Crie primeiro um save em {0}.", "Önce {0} içinde kayıt oluşturun.");
            Add(translations, ModTextKey.DetailNewSaveStartingBalance, "Choose the opening balance for the new save.", "Choisissez le solde de départ pour la nouvelle sauvegarde.", "Scegli il saldo iniziale per il nuovo salvataggio.", "Elige el saldo inicial para la nueva partida.", "Выберите стартовый баланс для нового сохранения.", "新しいセーブの開始資金を選択。", "为新存档选择初始资金。", "नई सेव के लिए शुरुआती बैलेंस चुनें।", "Escolha o saldo inicial do novo save.", "Yeni kayıt için başlangıç bakiyesini seçin.");
            Add(translations, ModTextKey.DetailSpeedUnit, "Switch the cruise-control speed display between imperial and metric units.", "Bascule l'affichage du régulateur entre impérial et métrique.", "Cambia l'affichage del cruise control tra imperiale e metrico.", "Cambia el indicador del control de crucero entre imperial y métrico.", "Переключает показ круиз-контроля между имперскими и метрическими единицами.", "クルーズコントロール表示をヤード・ポンド法とメートル法で切替。", "在英制和公制之间切换巡航控制速度显示。", "क्रूज़ कंट्रोल स्पीड डिस्प्ले को इम्पीरियल और मेट्रिक में बदलें।", "Troca a exibição do controle de cruzeiro entre imperial e métrico.", "Hız sabitleyici göstergesini imperial ve metrik arasında değiştirir.");
            Add(translations, ModTextKey.DetailVehicleFuel, "Enable vehicle fuel usage for cargo operations.", "Active l'usage du carburant pour les opérations cargo.", "Abilita il carburante per le operazioni cargo.", "Activa el combustible para operaciones de carga.", "Включает расход топлива для грузовых операций.", "貨物運用で燃料を使用。", "为货运操作启用燃料消耗。", "कार्गो ऑपरेशन के लिए वाहन ईंधन चालू करें।", "Ativa o uso de combustível nas operações de carga.", "Yük operasyonları için araç yakıtını etkinleştirir.");
            Add(translations, ModTextKey.DetailCargoWeightPower, "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.", "Reduce engine power as carried cargo weight increases.");
            Add(translations, ModTextKey.DetailCruiseControl, "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.", "Hold the current speed until you brake, disable it, or leave the vehicle.");
            Add(translations, ModTextKey.DetailCargoDamage, "Enable cargo loss and condition damage from collisions.", "Active la perte de cargaison et les dégâts dus aux collisions.", "Abilita perdita carico e danni da collisione.", "Activa pérdida de carga y daños por colisiones.", "Включает потерю груза и урон от столкновений.", "衝突による貨物損失と損傷を有効化。", "启用碰撞导致的货损和状态损伤。", "टक्कर से कार्गो हानि और क्षति चालू करें।", "Ativa perda de carga e dano por colisões.", "Çarpışmalardan yük kaybı ve hasarını etkinleştirir.");
            Add(translations, ModTextKey.DetailIndustryPriceMechanic, "Require industry purchases and owner-cut payouts until bought.", "Exige l'achat des industries et les commissions du propriétaire avant acquisition.", "Richiede acquisti industria e percentuale proprietario fino all'acquisto.", "Requiere comprar industrias y pagar comisión del dueño hasta adquirirlas.", "Требует покупку отрасли и выплаты владельцу до выкупа.", "購入まで産業の買収とオーナー取り分を要求。", "在购买前需要产业购买并支付所有者分成。", "खरीद तक इंडस्ट्री खरीद और मालिक हिस्सेदारी लागू करें।", "Exige compra da indústria e repasse ao dono até a aquisição.", "Satın alana kadar endüstri satın alma ve sahip payı ödemesi gerektirir.");
            Add(translations, ModTextKey.DetailLicensingSystem, "Require contractor permits before transporting cargo to or from industries.", "Exige des permis avant de transporter du cargo vers ou depuis les industries.", "Richiede permessi prima di trasportare carichi da o verso le industrie.", "Requiere permisos antes de transportar carga hacia o desde industrias.", "Требует разрешения перед перевозкой груза к отраслям и от них.", "産業への貨物輸送前に許可証が必要。", "在往返产业运输货物前需要许可证。", "इंडस्ट्री से या तक कार्गो ले जाने से पहले परमिट चाहिए।", "Exige licenças antes de transportar carga para ou das indústrias.", "Endüstrilere yük taşımadan önce izin gerektirir.");
            Add(translations, ModTextKey.DetailCorridorRestriction, "Require unlocked corridors before cross-district logistics routes can operate.", "Exige des corridors débloqués avant que les routes logistiques inter-districts puissent fonctionner.", "Richiede corridoi sbloccati prima che le rotte logistiche tra distretti possano operare.", "Requiere corredores desbloqueados antes de que puedan operar rutas logísticas entre distritos.", "Требует разблокированные коридоры, прежде чем межрайонные логистические маршруты смогут работать.", "地区間物流ルートを運用する前に、回廊の解放を要求します。", "跨区域物流路线运行前需要先解锁走廊。", "अंतर-जिला लॉजिस्टिक रूट चलाने से पहले कॉरिडोर अनलॉक होना जरूरी होगा।", "Exige corredores desbloqueados antes que rotas logísticas entre distritos possam operar.", "İlçeler arası lojistik rotaların çalışması için koridorların önce açılmış olmasını gerektirir.");
            Add(translations, ModTextKey.DetailReputationSystem, "Disable district reputation completely. Reputation gates, bonuses, penalties, and notices become inactive while off.", "Désactive totalement la réputation des districts. Les blocages, bonus, pénalités et notifications deviennent inactifs.", "Disattiva completamente la reputazione dei distretti. Blocchi, bonus, penalità e notifiche diventano inattivi.", "Desactiva por completo la reputación de distrito. Bloqueos, bonificaciones, penalizaciones y avisos quedan inactivos.", "Полностью отключает репутацию районов. Ограничения, бонусы, штрафы и уведомления становятся неактивны.", "地区の評判を完全に無効化。ゲート、ボーナス、ペナルティ、通知は停止します。", "完全禁用区域声望。相关门槛、奖励、惩罚和通知都会失效。", "डिस्ट्रिक्ट रिप्यूटेशन पूरी तरह बंद करता है। गेट, बोनस, पेनल्टी और नोटिस निष्क्रिय हो जाते हैं।", "Desativa totalmente a reputação distrital. Restrições, bônus, penalidades e avisos ficam inativos.", "Bölge itibarını tamamen kapatır. Kısıtlar, bonuslar, cezalar ve bildirimler devre dışı kalır.");
            Add(translations, ModTextKey.DetailOfficeGarageLimit, "Disable office garage capacity checks for purchases, storage, retrieval, spawning, and assignments.", "Désactive les limites du garage pour achat, stockage, sortie, spawn et affectation.", "Disattiva i controlli capienza garage per acquisti, deposito, ritiro, spawn e assegnazioni.", "Desactiva los límites del garaje para compra, guardado, retirada, aparición y asignación.", "Отключает лимит гаража офиса для покупки, хранения, выдачи, спавна и назначений.", "購入、保管、取出し、スポーン、割当でオフィスガレージ上限を無効化。", "关闭办公室车库容量检查，适用于购买、存放、取车、生成和分配。", "खरीद, स्टोरेज, रिट्रीवल, स्पॉन और असाइनमेंट के लिए ऑफिस गैरेज सीमा जांच बंद करें।", "Desativa os limites da garagem do escritório para compra, armazenamento, retirada, spawn e atribuição.", "Satın alma, depolama, çağırma, doğma ve atamalar için ofis garaj kapasite kontrollerini kapatır.");
            Add(translations, ModTextKey.DetailNpcRouteLimit, "Set the maximum route slots per hired NPC. 0 disables Hiring NPC entirely.", "Définit le nombre maximum de routes par PNJ recruté. 0 désactive le recrutement PNJ.", "Imposta il numero massimo di rotte per ogni NPC assunto. 0 disattiva il reclutamento NPC.", "Define el máximo de rutas por NPC contratado. 0 desactiva por completo los NPC contratados.", "Задаёт максимум маршрутов на одного нанятого NPC. 0 полностью отключает найм NPC.", "雇用NPCあたりの最大ルート数を設定。0で雇用NPCを完全無効化。", "设置每个雇佣 NPC 的最大路线槽。0 会完全禁用雇佣 NPC。", "प्रति हायर्ड NPC अधिकतम रूट स्लॉट सेट करें। 0 पर Hiring NPC पूरी तरह बंद हो जाता है।", "Define o máximo de rotas por NPC contratado. 0 desativa totalmente o Hiring NPC.", "Kiralanan NPC başına en fazla rota sayısını ayarlar. 0, NPC işe alımını tamamen kapatır.");
            Add(translations, ModTextKey.DetailCreateSave, "Starts a fresh game as {0}.state.ini", "Démarre une nouvelle partie sous {0}.state.ini", "Avvia una nuova partita come {0}.state.ini", "Inicia una nueva partida como {0}.state.ini", "Запускает новую игру как {0}.state.ini", "{0}.state.ini として新規開始", "以 {0}.state.ini 开始新游戏", "{0}.state.ini के रूप में नई गेम शुरू करता है", "Inicia um novo jogo como {0}.state.ini", "Yeni oyunu {0}.state.ini olarak başlatır");
            Add(translations, ModTextKey.DetailLanguage, "Apply language changes immediately across localized menu and status text.", "Applique immédiatement la langue aux menus et statuts localisés.", "Applica subito la lingua a menu e stati localizzati.", "Aplica de inmediato el idioma a menús y estados localizados.", "Сразу применяет язык к локализованным меню и статусам.", "ローカライズ済みのメニューと状態表示に即時適用。", "立即将语言应用到已本地化的菜单和状态文本。", "लोकलाइज़्ड मेनू और स्टेटस टेक्स्ट पर भाषा तुरंत लागू करें।", "Aplica imediatamente o idioma a menus e textos de status localizados.", "Yerelleştirilmiş menü ve durum yazılarına dili hemen uygular.");
            Add(translations, ModTextKey.DetailLanguageEnglishFallback, "English is active. Left or right switches to a translated language.", "Le secours anglais est actif. Gauche ou droite passe à une langue traduite.", "Il fallback inglese è attivo. Sinistra o destra passa a una lingua tradotta.", "El inglés de respaldo está activo. Izquierda o derecha cambia a un idioma traducido.", "Активен английский запасной язык. Влево или вправо переключает на перевод.", "英語フォールバックが有効です。左右で翻訳言語に切替。", "当前使用英文回退。左右可切换到翻译语言。", "अभी अंग्रेज़ी फ़ॉलबैक सक्रिय है। बाएँ या दाएँ से अनुवादित भाषा चुनें।", "O fallback em inglês está ativo. Esquerda ou direita troca para um idioma traduzido.", "İngilizce yedek dil etkin. Sağ veya sol ile çevrilmiş dillere geçin.");
            Add(translations, ModTextKey.DetailSpeedUnitChanged, "Speed units set to {0}.", "Unités de vitesse réglées sur {0}.", "Unità velocità impostate su {0}.", "Unidades de velocidad establecidas en {0}.", "Единицы скорости установлены: {0}.", "速度単位を {0} に設定。", "速度单位已设置为 {0}。", "स्पीड यूनिट {0} पर सेट हुई।", "Unidades de velocidade definidas para {0}.", "Hız birimleri {0} olarak ayarlandı.");
            Add(translations, ModTextKey.DetailColorblindMode, "Apply a high-contrast mod UI palette tuned for the selected color vision mode.", "Applique une palette à fort contraste adaptée au mode de vision sélectionné.", "Applica una palette ad alto contrasto per la modalità visiva selezionata.", "Aplica una paleta de alto contraste según la visión seleccionada.", "Применяет высококонтрастную палитру интерфейса под выбранный режим зрения.", "選択した色覚モード向けの高コントラストUI配色を適用。", "应用适合所选色觉模式的高对比度模组界面配色。", "चुने गए रंग दृष्टि मोड के लिए हाई-कॉन्ट्रास्ट UI पैलेट लागू करें।", "Aplica uma paleta de alto contraste ajustada ao modo de visão selecionado.", "Seçilen renk görme moduna göre yüksek kontrastlı arayüz paleti uygular.");
            Add(translations, ModTextKey.DetailOptions, "Adjust language, speed units, and accessibility options.", "Ajustez la langue et les options d'accessibilité.", "Regola lingua e accessibilità.", "Ajusta idioma y accesibilidad.", "Настройте язык и доступность.", "言語とアクセシビリティを調整。", "调整语言和辅助功能。", "भाषा और एक्सेसिबिलिटी विकल्प बदलें।", "Ajuste idioma e acessibilidade.", "Dil ve erişilebilirlik seçeneklerini ayarlayın.");
            Add(translations, ModTextKey.DetailOptionsNavigate, "Left or right cycles the available options.", "Gauche ou droite fait défiler les options.", "Sinistra o destra scorrono le opzioni.", "Izquierda o derecha recorren las opciones.", "Влево или вправо переключают варианты.", "左右で選択肢を切替。", "左右切换可用选项。", "बाएँ या दाएँ से विकल्प बदलें।", "Esquerda ou direita percorrem as opções.", "Sağ veya sol mevcut seçenekleri değiştirir.");
            Add(translations, ModTextKey.DetailDifficultySettingsLockedStatus, "Difficulty settings are sealed for this save. Create a new save to change them.", "Les paramètres de difficulté sont scellés pour cette sauvegarde. Créez une nouvelle sauvegarde pour les modifier.", "Le impostazioni difficoltà sono bloccate per questo salvataggio. Crea un nuovo salvataggio per cambiarle.", "Los ajustes de dificultad están sellados para esta partida. Crea una nueva partida para cambiarlos.", "Настройки сложности зафиксированы для этого сохранения. Создайте новое сохранение, чтобы изменить их.", "このセーブでは難易度設定が固定です。変更するには新しいセーブを作成してください。", "此存档的难度设置已锁定。请创建新存档来更改。", "इस सेव के लिए कठिनाई सेटिंग्स सील हैं। बदलने के लिए नई सेव बनाएं।", "As configurações de dificuldade estão seladas para este save. Crie um novo save para alterá-las.", "Bu kayıt için zorluk ayarları mühürlü. Değiştirmek için yeni kayıt oluşturun.");
            Add(translations, ModTextKey.DetailPersistenceEnabled, "Industry persistence enabled.", "Persistance des industries activée.", "Persistenza industrie attivata.", "Persistencia industrial activada.", "Сохранение отраслей включено.", "産業保存を有効化。", "产业持久化已启用。", "इंडस्ट्री पर्सिस्टेंस चालू।", "Persistência da indústria ativada.", "Endüstri kalıcılığı etkinleştirildi.");
            Add(translations, ModTextKey.DetailPersistenceDisabled, "Industry persistence disabled.", "Persistance des industries désactivée.", "Persistenza industrie disattivata.", "Persistencia industrial desactivada.", "Сохранение отраслей выключено.", "産業保存を無効化。", "产业持久化已禁用。", "इंडस्ट्री पर्सिस्टेंस बंद।", "Persistência da indústria desativada.", "Endüstri kalıcılığı devre dışı.");
            Add(translations, ModTextKey.DetailDebugUnavailable, "Debug tools are only available while a debugger is attached.", "Les outils debug ne sont disponibles qu'avec un débogueur attaché.", "Gli strumenti debug sono disponibili solo con un debugger collegato.", "Las herramientas de depuración solo están disponibles con un depurador conectado.", "Инструменты отладки доступны только при подключенном отладчике.", "デバッグツールはデバッガ接続時のみ使用可能。", "仅在附加调试器时可用调试工具。", "डिबग टूल केवल डिबगर जुड़ा होने पर उपलब्ध हैं।", "Ferramentas de depuração só ficam disponíveis com um depurador anexado.", "Hata ayıklama araçları yalnızca bir debugger bağlıyken kullanılabilir.");
            Add(translations, ModTextKey.DetailLoadSavedState, "Loaded saved industry state for {0} nodes.", "État industriel chargé pour {0} nœuds.", "Stato industria caricato per {0} nodi.", "Estado industrial cargado para {0} nodos.", "Загружено состояние отрасли для {0} узлов.", "{0} ノードの産業状態を読込。", "已加载 {0} 个节点的产业状态。", "{0} नोड्स के लिए इंडस्ट्री स्टेट लोड हुआ।", "Estado da indústria carregado para {0} nós.", "{0} düğüm için kayıtlı endüstri durumu yüklendi.");
            Add(translations, ModTextKey.DetailLoadSavedSettings, "Loaded saved game settings.", "Paramètres de jeu chargés.", "Impostazioni di gioco caricate.", "Se cargaron los ajustes guardados.", "Настройки игры загружены.", "保存済み設定を読込。", "已加载存档设置。", "सेव गेम सेटिंग्स लोड हुईं।", "Configurações salvas carregadas.", "Kayıtlı oyun ayarları yüklendi.");
            Add(translations, ModTextKey.DetailNoSavedState, "No saved industry state found yet.", "Aucun état industriel sauvegardé pour le moment.", "Nessuno stato industria salvato trovato.", "Aún no se encontró estado industrial guardado.", "Сохранённое состояние отрасли пока не найдено.", "保存された産業状態はまだありません。", "尚未找到已保存的产业状态。", "अभी कोई सेव इंडस्ट्री स्टेट नहीं मिला।", "Nenhum estado da indústria salvo encontrado ainda.", "Henüz kayıtlı endüstri durumu bulunamadı.");
            Add(translations, ModTextKey.DetailNoSavePath, "No save path is available.", "Aucun chemin de sauvegarde disponible.", "Nessun percorso di salvataggio disponibile.", "No hay ruta de guardado disponible.", "Путь сохранения недоступен.", "保存先がありません。", "没有可用的保存路径。", "कोई सेव पाथ उपलब्ध नहीं है।", "Nenhum caminho de save disponível.", "Kullanılabilir kayıt yolu yok.");
            Add(translations, ModTextKey.DetailSaveCreationCancelled, "Save creation cancelled.", "Création de sauvegarde annulée.", "Creazione salvataggio annullata.", "Creación de partida cancelada.", "Создание сохранения отменено.", "セーブ作成を中止。", "已取消创建存档。", "सेव बनाना रद्द।", "Criação de save cancelada.", "Kayıt oluşturma iptal edildi.");
            Add(translations, ModTextKey.DetailSaveNameInvalid, "Enter a valid save name.", "Entrez un nom de sauvegarde valide.", "Inserisci un nome salvataggio valido.", "Introduce un nombre de partida válido.", "Введите корректное имя сохранения.", "有効なセーブ名を入力。", "请输入有效的存档名称。", "मान्य सेव नाम दर्ज करें।", "Digite um nome de save válido.", "Geçerli bir kayıt adı girin.");
            Add(translations, ModTextKey.DetailSaveExists, "A save with that name already exists.", "Une sauvegarde avec ce nom existe déjà.", "Esiste già un salvataggio con questo nome.", "Ya existe una partida con ese nombre.", "Сохранение с таким именем уже существует.", "その名前のセーブは既に存在します。", "该名称的存档已存在。", "इस नाम की सेव पहले से मौजूद है।", "Já existe um save com esse nome.", "Bu adda bir kayıt zaten var.");
            Add(translations, ModTextKey.DetailNoPendingSaveName, "No save name selected.", "Aucun nom de sauvegarde sélectionné.", "Nessun nome salvataggio selezionato.", "No hay nombre de partida seleccionado.", "Имя сохранения не выбрано.", "セーブ名が未選択です。", "未选择存档名称。", "कोई सेव नाम चुना नहीं गया।", "Nenhum nome de save selecionado.", "Seçili bir kayıt adı yok.");
            Add(translations, ModTextKey.DetailSelectedSaveMissing, "Selected save was not found.", "La sauvegarde sélectionnée est introuvable.", "Il salvataggio selezionato non è stato trovato.", "No se encontró la partida seleccionada.", "Выбранное сохранение не найдено.", "選択したセーブが見つかりません。", "未找到所选存档。", "चुनी गई सेव नहीं मिली।", "O save selecionado não foi encontrado.", "Seçilen kayıt bulunamadı.");
            Add(translations, ModTextKey.DetailCreateOrLoadNamedSave, "Create or load a named save first.", "Créez ou chargez d'abord une sauvegarde nommée.", "Crea o carica prima un salvataggio con nome.", "Crea o carga primero una partida con nombre.", "Сначала создайте или загрузите именованное сохранение.", "先に名前付きセーブを作成または読込。", "请先创建或加载命名存档。", "पहले नामित सेव बनाएं या लोड करें।", "Crie ou carregue um save nomeado primeiro.", "Önce adlandırılmış bir kayıt oluşturun veya yükleyin.");
            Add(translations, ModTextKey.DetailSaveCreated, "Created save '{0}'.", "Sauvegarde '{0}' créée.", "Salvataggio '{0}' creato.", "Partida '{0}' creada.", "Сохранение '{0}' создано.", "セーブ '{0}' を作成。", "已创建存档“{0}”。", "सेव '{0}' बनाई गई।", "Save '{0}' criado.", "'{0}' kaydı oluşturuldu.");
            Add(translations, ModTextKey.DetailSaveLoaded, "Loaded save '{0}'.", "Sauvegarde '{0}' chargée.", "Salvataggio '{0}' caricato.", "Partida '{0}' cargada.", "Сохранение '{0}' загружено.", "セーブ '{0}' を読込。", "已加载存档“{0}”。", "सेव '{0}' लोड हुई।", "Save '{0}' carregado.", "'{0}' kaydı yüklendi.");
            Add(translations, ModTextKey.DetailSaveDeleted, "Deleted save '{0}'.", "Sauvegarde '{0}' supprimée.", "Salvataggio '{0}' eliminato.", "Partida '{0}' eliminada.", "Сохранение '{0}' удалено.", "セーブ '{0}' を削除。", "已删除存档“{0}”。", "सेव '{0}' हटाई गई।", "Save '{0}' excluído.", "'{0}' kaydı silindi.");
            Add(translations, ModTextKey.DetailSaveSaved, "Saved '{0}'.", "'{0}' sauvegardé.", "'{0}' salvato.", "'{0}' guardado.", "'{0}' сохранено.", "'{0}' を保存。", "已保存“{0}”。", "'{0}' सहेजा गया।", "'{0}' salvo.", "'{0}' kaydedildi.");
            Add(translations, ModTextKey.DetailLoadedSuccessfully, "Loaded successfully.", "Chargé avec succès.", "Caricato con successo.", "Cargado correctamente.", "Успешно загружено.", "正常に読み込みました。", "加载成功。", "सफलतापूर्वक लोड हुआ।", "Carregado com sucesso.", "Başarıyla yüklendi.");
            Add(translations, ModTextKey.DetailMechanicsEnabled, "Mod mechanics enabled.", "Mécaniques du mod activées.", "Meccaniche mod attivate.", "Mecánicas del mod activadas.", "Механики мода включены.", "MOD機能を有効化。", "模组机制已启用。", "मॉड मेकैनिक्स चालू।", "Mecânicas do mod ativadas.", "Mod mekanikleri etkinleştirildi.");
            Add(translations, ModTextKey.DetailMechanicsDisabled, "Mod mechanics disabled.", "Mécaniques du mod désactivées.", "Meccaniche mod disattivate.", "Mecánicas del mod desactivadas.", "Механики мода отключены.", "MOD機能を無効化。", "模组机制已禁用。", "मॉड मेकैनिक्स बंद।", "Mecânicas do mod desativadas.", "Mod mekanikleri devre dışı.");
            Add(translations, ModTextKey.DetailLanguageChanged, "Language set to {0}.", "Langue réglée sur {0}.", "Lingua impostata su {0}.", "Idioma configurado en {0}.", "Язык изменён на {0}.", "言語を {0} に設定。", "语言已设置为 {0}。", "भाषा {0} पर सेट की गई।", "Idioma definido para {0}.", "Dil {0} olarak ayarlandı.");
            Add(translations, ModTextKey.DetailColorblindChanged, "Colorblind mode set to {0}.", "Mode daltonien réglé sur {0}.", "Modalità daltonismo impostata su {0}.", "Modo daltónico configurado en {0}.", "Режим дальтонизма: {0}.", "色覚サポートを {0} に設定。", "色盲模式已设置为 {0}。", "कलरब्लाइंड मोड {0} पर सेट किया गया।", "Modo daltônico definido para {0}.", "Renk körü modu {0} olarak ayarlandı.");
            Add(translations, ModTextKey.DetailEconomyPresetCasual, "Industry price $200,000 | licence $8,000 | 180t input | 150t output | base 40 cyc/h. OmegaFactory x1.75, RecyclingCenter x3.00.", "Prix industrie 200 000 $ | licence 8 000 $ | entrée 180t | sortie 150t | base 40 cyc/h. OmegaFactory x1,75, RecyclingCenter x3,00.", "Prezzo industria $200.000 | licenza $8.000 | input 180t | output 150t | base 40 cic/h. OmegaFactory x1,75, RecyclingCenter x3,00.", "Precio industria $200,000 | licencia $8,000 | entrada 180t | salida 150t | base 40 cic/h. OmegaFactory x1.75, RecyclingCenter x3.00.", "Цена отрасли $200,000 | лицензия $8,000 | вход 180т | выход 150т | база 40 цик/ч. OmegaFactory x1.75, RecyclingCenter x3.00.", "産業価格 $200,000 | ライセンス $8,000 | 入力180t | 出力150t | 基本40 cyc/h。OmegaFactory x1.75、RecyclingCenter x3.00。", "产业价格 $200,000 | 许可证 $8,000 | 输入 180t | 输出 150t | 基础 40 cyc/h。OmegaFactory x1.75，RecyclingCenter x3.00。", "इंडस्ट्री कीमत $200,000 | लाइसेंस $8,000 | 180t इनपुट | 150t आउटपुट | बेस 40 cyc/h. OmegaFactory x1.75, RecyclingCenter x3.00.", "Preço da indústria $200.000 | licença $8.000 | entrada 180t | saída 150t | base 40 cyc/h. OmegaFactory x1,75, RecyclingCenter x3,00.", "Endüstri fiyatı $200.000 | lisans $8.000 | 180t girdi | 150t çıktı | taban 40 döngü/s. OmegaFactory x1,75, RecyclingCenter x3,00.");
            Add(translations, ModTextKey.DetailEconomyPresetStandard, "Industry price $450,000 | licence $13,000 | 120t input | 100t output | base 32 cyc/h. OmegaFactory x1.50, RecyclingCenter x2.50.", "Prix industrie 450 000 $ | licence 13 000 $ | entrée 120t | sortie 100t | base 32 cyc/h. OmegaFactory x1,50, RecyclingCenter x2,50.", "Prezzo industria $450.000 | licenza $13.000 | input 120t | output 100t | base 32 cic/h. OmegaFactory x1,50, RecyclingCenter x2,50.", "Precio industria $450,000 | licencia $13,000 | entrada 120t | salida 100t | base 32 cic/h. OmegaFactory x1.50, RecyclingCenter x2.50.", "Цена отрасли $450,000 | лицензия $13,000 | вход 120т | выход 100т | база 32 цик/ч. OmegaFactory x1.50, RecyclingCenter x2.50.", "産業価格 $450,000 | ライセンス $13,000 | 入力120t | 出力100t | 基本32 cyc/h。OmegaFactory x1.50、RecyclingCenter x2.50。", "产业价格 $450,000 | 许可证 $13,000 | 输入 120t | 输出 100t | 基础 32 cyc/h。OmegaFactory x1.50，RecyclingCenter x2.50。", "इंडस्ट्री कीमत $450,000 | लाइसेंस $13,000 | 120t इनपुट | 100t आउटपुट | बेस 32 cyc/h. OmegaFactory x1.50, RecyclingCenter x2.50.", "Preço da indústria $450.000 | licença $13.000 | entrada 120t | saída 100t | base 32 cyc/h. OmegaFactory x1,50, RecyclingCenter x2,50.", "Endüstri fiyatı $450.000 | lisans $13.000 | 120t girdi | 100t çıktı | taban 32 döngü/s. OmegaFactory x1,50, RecyclingCenter x2,50.");
            Add(translations, ModTextKey.DetailEconomyPresetHardcore, "Industry price $800,000 | licence $18,000 | 80t input | 70t output | base 24 cyc/h. OmegaFactory x1.25, RecyclingCenter x2.00.", "Prix industrie 800 000 $ | licence 18 000 $ | entrée 80t | sortie 70t | base 24 cyc/h. OmegaFactory x1,25, RecyclingCenter x2,00.", "Prezzo industria $800.000 | licenza $18.000 | input 80t | output 70t | base 24 cic/h. OmegaFactory x1,25, RecyclingCenter x2,00.", "Precio industria $800,000 | licencia $18,000 | entrada 80t | salida 70t | base 24 cic/h. OmegaFactory x1.25, RecyclingCenter x2.00.", "Цена отрасли $800,000 | лицензия $18,000 | вход 80т | выход 70т | база 24 цик/ч. OmegaFactory x1.25, RecyclingCenter x2.00.", "産業価格 $800,000 | ライセンス $18,000 | 入力80t | 出力70t | 基本24 cyc/h。OmegaFactory x1.25、RecyclingCenter x2.00。", "产业价格 $800,000 | 许可证 $18,000 | 输入 80t | 输出 70t | 基础 24 cyc/h。OmegaFactory x1.25，RecyclingCenter x2.00。", "इंडस्ट्री कीमत $800,000 | लाइसेंस $18,000 | 80t इनपुट | 70t आउटपुट | बेस 24 cyc/h. OmegaFactory x1.25, RecyclingCenter x2.00.", "Preço da indústria $800.000 | licença $18.000 | entrada 80t | saída 70t | base 24 cyc/h. OmegaFactory x1,25, RecyclingCenter x2,00.", "Endüstri fiyatı $800.000 | lisans $18.000 | 80t girdi | 70t çıktı | taban 24 döngü/s. OmegaFactory x1,25, RecyclingCenter x2,00.");
            Add(translations, ModTextKey.DetailNpcWeeklyPayroll, "Weekly NPC payroll only. Rookie {0} | Pro {1} | Veteran {2}.", "Paie hebdo PNJ seulement. Débutant {0} | Pro {1} | Vétéran {2}.", "Solo paga settimanale NPC. Rookie {0} | Pro {1} | Veterano {2}.", "Solo nómina semanal NPC. Novato {0} | Pro {1} | Veterano {2}.", "Только недельная зарплата NPC. Новичок {0} | Профи {1} | Ветеран {2}.", "NPC週給のみ。Rookie {0} | Pro {1} | Veteran {2}。", "仅 NPC 周薪。新手 {0} | 专业 {1} | 老兵 {2}。", "केवल NPC साप्ताहिक पेरोल। Rookie {0} | Pro {1} | Veteran {2}.", "Apenas folha semanal dos NPCs. Novato {0} | Pro {1} | Veterano {2}.", "Yalnızca haftalık NPC maaşı. Çaylak {0} | Pro {1} | Kıdemli {2}.");

            Add(translations, ModTextKey.LemonToggleHint, "Press Enter to toggle.", "Appuyez sur Entrée pour basculer.", "Premi Invio per attivare.", "Pulsa Intro para alternar.", "Нажмите Enter для переключения.", "Enterで切替。", "按 Enter 切换。", "टॉगल करने के लिए Enter दबाएं।", "Pressione Enter para alternar.", "Açıp kapatmak için Enter'a basın.");
            Add(translations, ModTextKey.LemonAdjustHint, "Left/Right to adjust.", "Gauche/Droite pour ajuster.", "Sinistra/Destra per regolare.", "Izquierda/Derecha para ajustar.", "Влево/вправо для изменения.", "左右で調整。", "左右调整。", "समायोजित करने के लिए बाएँ/दाएँ।", "Esquerda/Direita para ajustar.", "Ayarlamak için Sol/Sağ.");
            Add(translations, ModTextKey.LemonCycleHint, "Left/Right to cycle options.", "Gauche/Droite pour faire défiler les options.", "Sinistra/Destra per scorrere le opzioni.", "Izquierda/Derecha para cambiar opciones.", "Влево/вправо для выбора вариантов.", "左右で項目切替。", "左右切换选项。", "विकल्प बदलने के लिए बाएँ/दाएँ।", "Esquerda/Direita para percorrer opções.", "Seçenekler arasında geçmek için Sol/Sağ.");
            Add(translations, ModTextKey.LemonUseHint, "Press Enter to use this option.", "Appuyez sur Entrée pour utiliser cette option.", "Premi Invio per usare questa opzione.", "Pulsa Intro para usar esta opción.", "Нажмите Enter, чтобы использовать этот пункт.", "Enterで使用。", "按 Enter 使用此选项。", "इस विकल्प का उपयोग करने के लिए Enter दबाएं।", "Pressione Enter para usar esta opção.", "Bu seçeneği kullanmak için Enter'a basın.");

            Add(translations, ModTextKey.ValueDefaultAutosave, "Default autosave", "Sauvegarde auto par défaut", "Autosalvataggio predefinito", "Autoguardado predeterminado", "Автосохранение по умолчанию", "既定のオートセーブ", "默认自动存档", "डिफ़ॉल्ट ऑटोसेव", "Autossalvamento padrão", "Varsayılan otomatik kayıt");
            Add(translations, ModTextKey.ValueDifficultyCasual, "Casual", "Détendu", "Casual", "Casual", "Лёгкий", "カジュアル", "休闲", "कैज़ुअल", "Casual", "Rahat");
            Add(translations, ModTextKey.ValueDifficultyStandard, "Standard", "Standard", "Standard", "Estándar", "Стандарт", "標準", "标准", "स्टैंडर्ड", "Padrão", "Standart");
            Add(translations, ModTextKey.ValueDifficultyHardcore, "Hardcore", "Hardcore", "Hardcore", "Hardcore", "Хардкор", "ハードコア", "硬核", "हार्डकोर", "Hardcore", "Zorlayıcı");

            Add(translations, ModTextKey.ValueLanguageEnglishFallback, "English", "Anglais", "Fallback inglese", "Inglés de respaldo", "Резервный английский", "英語フォールバック", "英文回退", "अंग्रेज़ी फ़ॉलबैक", "Inglês de fallback", "İngilizce yedek");
            Add(translations, ModTextKey.ValueLanguageFrench, "French", "Français", "Francese", "Francés", "Французский", "フランス語", "法语", "फ़्रेंच", "Francês", "Fransızca");
            Add(translations, ModTextKey.ValueLanguageItalian, "Italian", "Italien", "Italiano", "Italiano", "Итальянский", "イタリア語", "意大利语", "इटैलियन", "Italiano", "İtalyanca");
            Add(translations, ModTextKey.ValueLanguageSpanish, "Spanish", "Espagnol", "Spagnolo", "Español", "Испанский", "スペイン語", "西班牙语", "स्पैनिश", "Espanhol", "İspanyolca");
            Add(translations, ModTextKey.ValueLanguageRussian, "Russian", "Russe", "Russo", "Ruso", "Русский", "ロシア語", "俄语", "रशियन", "Russo", "Rusça");
            Add(translations, ModTextKey.ValueLanguageJapanese, "Japanese", "Japonais", "Giapponese", "Japonés", "Японский", "日本語", "日语", "जापानी", "Japonês", "Japonca");
            Add(translations, ModTextKey.ValueLanguageChinese, "Chinese", "Chinois", "Cinese", "Chino", "Китайский", "中国語", "中文", "चीनी", "Chinês", "Çince");
            Add(translations, ModTextKey.ValueLanguageHindi, "Hindi", "Hindi", "Hindi", "Hindi", "Хинди", "ヒンディー語", "印地语", "हिंदी", "Hindi", "Hintçe");
            Add(translations, ModTextKey.ValueLanguagePortuguese, "Portuguese", "Portugais", "Portoghese", "Portugués", "Португальский", "ポルトガル語", "葡萄牙语", "पुर्तगाली", "Português", "Portekizce");
            Add(translations, ModTextKey.ValueLanguageTurkish, "Turkish", "Turc", "Turco", "Turco", "Турецкий", "トルコ語", "土耳其语", "तुर्की", "Turco", "Türkçe");

            Add(translations, ModTextKey.ValueUnitImperial, "Imperial", "Impérial", "Imperiale", "Imperial", "Имперские", "ヤード・ポンド法", "英制", "इम्पीरियल", "Imperial", "İmperyal");
            Add(translations, ModTextKey.ValueUnitMetric, "Metric", "Métrique", "Metrico", "Métrico", "Метрические", "メートル法", "公制", "मेट्रिक", "Métrico", "Metrik");

            Add(translations, ModTextKey.ValueColorblindOff, "Off", "Désactivé", "Disattivato", "Desactivado", "Выкл.", "オフ", "关", "बंद", "Desligado", "Kapalı");
            Add(translations, ModTextKey.ValueColorblindDeuteranopia, "Deuteranopia", "Deutéranopie", "Deuteranopia", "Deuteranopía", "Дейтеранопия", "2型色覚", "绿色盲", "ड्यूटेरानोपिया", "Deuteranopia", "Döteranopi");
            Add(translations, ModTextKey.ValueColorblindProtanopia, "Protanopia", "Protanopie", "Protanopia", "Protanopía", "Протанопия", "1型色覚", "红色盲", "प्रोटैनोपिया", "Protanopia", "Protanopi");
            Add(translations, ModTextKey.ValueColorblindTritanopia, "Tritanopia", "Tritanopie", "Tritanopia", "Tritanopía", "Тританопия", "3型色覚", "蓝黄色盲", "ट्राइटैनोपिया", "Tritanopia", "Tritanopi");

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
            string japanese,
            string chinese,
            string hindi,
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
                { ModLanguage.Japanese, japanese },
                { ModLanguage.Chinese, chinese },
                { ModLanguage.Hindi, hindi },
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