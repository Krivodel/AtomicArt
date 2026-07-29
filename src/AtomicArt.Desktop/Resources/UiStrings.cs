namespace AtomicArt.Desktop.Resources;

public static class UiStrings
{
    public static string AppTitle => "Atomic Art";
    public static string PromptPlaceholder => "Сгенерируй новый сезон Рика и Морти...";
    public static string GenerateButtonText => "Сгенерировать";
    public static string GenerateButtonFormat => "Сгенерировать ({0})";
    public static string ShowWindow => "Показать окно";
    public static string Exit => "Выход";
    public static string SettingsTitle => "Настройки";
    public static string GalleryPlaceholder => "В галерее пусто";
    public static string Error => "Ошибка";
    public static string Copy => "Копировать";
    public static string UnhandledExceptionMessage => "Неизвестная ошибка.";
    public static string AttachmentCounterFormat => "{0}/{1}";
    public static string TemperatureValueFormat => "{0:0.0}";
    public static string PickImagesTitle => "Выбор изображений";
    public static string ImageAttachmentFailed => "Не удалось прикрепить.";
    public static string GenerationApiUnavailable => "Сервер недоступен.";
    public static string GenerationFailed => "Не удалось сгенерировать.";
    public static string GenerationAuthenticationFailed =>
        "Провайдер отклонил API-ключ. Проверь ключ в настройках.";
    public static string GenerationAuthorizationFailed =>
        "У API-ключа нет доступа к выбранной модели.";
    public static string GenerationRateLimited =>
        "Провайдер временно ограничил количество запросов. Попробуй позже.";
    public static string GenerationInvalidResponse =>
        "Провайдер вернул некорректный ответ.";
    public static string GenerationTimedOut =>
        "Провайдер не успел завершить генерацию вовремя.";
    public static string GenerationProviderUnavailable =>
        "Сервис провайдера временно недоступен. Попробуй позже.";
    public static string GenerationRequestRejected =>
        "Провайдер отклонил запрос. Проверь промпт, параметры и вложения.";
    public static string GenerationResourceNotFound =>
        "Выбранная модель недоступна у провайдера.";
    public static string GenerationProviderInternalError =>
        "У провайдера произошла внутренняя ошибка. Попробуй позже.";
    public static string GenerationConcurrencyLimitReached =>
        "Достигнут лимит одновременных генераций.";
    public static string GenerationInvalidRequest =>
        "Сервер не смог обработать запрос генерации.";
    public static string GenerationInvalidAttempt =>
        "Сервер отклонил номер попытки генерации.";
    public static string GenerationInvalidParameters =>
        "Параметры генерации не прошли проверку.";
    public static string GenerationResponseTooLarge =>
        "Результат генерации превысил допустимый размер.";
    public static string GenerationTransportInterrupted =>
        "Соединение прервалось во время получения результата.";
    public static string GoogleApiKeyMissing => "Укажи Google API-ключ в настройках.";
    public static string ModelCatalogLoadFailed => "Не удалось загрузить список моделей.";
    public static string ImageDropLabel => "ДРОП";
    public static string DeleteGlyph => "×";
    public static string CancelGlyph => "■";
    public static string MetadataTitle => "Свойства";
    public static string MetadataCreatedAt => "Дата";
    public static string MetadataPrompt => "ПРОМПТ";
    public static string MetadataModel => "Модель";
    public static string MetadataResolution => "Разрешение";
    public static string MetadataAspectRatio => "Соотношение";
    public static string MetadataAttachments => "Вложения";
    public static string MetadataPrice => "Цена";
    public static string MetadataGenerationDuration => "Время генерации";
    public static string MetadataImagePath => "Путь";
    public static string MetadataStatus => "Статус";
    public static string MetadataNoFilePath => "Нет файла";
    public static string MetadataUnavailable => "Недоступно";
    public static string MetadataRepeat => "Повторить";
    public static string FileRevealFailed => "Не удалось показать файл в папке.";
    public static string SettingsConnectionSection => "Подключение";
    public static string SettingsAppearanceSection => "Интерфейс";
    public static string SettingsStorageAndPerformanceSection => "Прочее";
    public static string SettingsApiBaseAddressLabel => "Адрес сервера";
    public static string SettingsApiBaseAddressPlaceholder => "http://localhost:5000/";
    public static string SettingsApiBaseAddressInvalid => "Укажи абсолютный адрес HTTP или HTTPS.";
    public static string SettingsGoogleApiKeyLabel => "Google API-ключ";
    public static string SettingsGoogleApiKeyPlaceholder => SettingsGoogleApiKeyLabel;
    public static string SettingsScaleLabel => "Масштаб";
    public static string SettingsPromptTextSizeLabel => "Размер текста промпта";
    public static string SettingsGpuResourceCacheLabel => "GPU-кэш";
    public static string SettingsGpuResourceCacheRestartNotice => "Кэш применится после перезапуска.";
    public static string SettingsDataRootLabel => "Папка сохранений";
    public static string SettingsDataRootPickerTitle => "Выбор пустой папки для сохранений Atomic Art";
    public static string SettingsDataRootPreparing => "Подготовка…";
    public static string SettingsDataRootCopying => "Копирование…";
    public static string SettingsDataRootVerifying => "Проверка…";
    public static string SettingsDataRootSwitching => "Переключение папки…";
    public static string SettingsDataRootCleaning => "Очистка прежней папки…";
    public static string SettingsDataRootCompleted => "Перенос завершён.";
    public static string SettingsDataRootMigrationFailed =>
        "Не удалось перенести сохранения. Проверь, что папка пуста, доступна для записи и на диске достаточно места. Прежняя папка продолжает использоваться.";
    public static string SettingsDataRootCleanupFailed =>
        "Новая папка уже используется, но прежнюю не удалось полностью очистить. Очистка будет повторена при следующем запуске.";
    public static string SettingsSaveSecretFailed => "Не удалось сохранить ключ.";
    public static string UpdateTitle => "Доступно обновление";
    public static string UpdateAvailableFormat => "Обновление {0} готово к установке.";
    public static string UpdateInstall => "Обновить";
    public static string UpdateWaitAndInstall => "Дождаться и обновить";
    public static string UpdateLater => "Не сейчас";
    public static string UpdateWaitingForGeneration => "Обновление начнётся после завершения генерации.";
    public static string UpdateDownloading => "Скачивание обновления…";
    public static string UpdateRestarting => "Обновление скачано. Установка…";
    public static string UpdateCheckFailed => "Не удалось проверить наличие обновлений.";
    public static string UpdateInstallFailed => "Не удалось установить обновление.";
}
