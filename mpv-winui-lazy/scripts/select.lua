--[[
This file is part of mpv.

mpv is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

mpv is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with mpv.  If not, see <http://www.gnu.org/licenses/>.
]]

local utils = require "mp.utils"
local input = require "mp.input"

local options = {
    history_date_format = "%Y-%m-%d %H:%M:%S",
    hide_history_duplicates = true,
}

require "mp.options".read_options(options, nil, function () end)

-- ===== 菜单本地化（跟随 App 界面语言 user-data/mpvw/language） =====
local select_i18n = {
    ["en-US"] = {
        ["播放列表为空。"] = "Playlist is empty.",
        ["选择播放列表条目："] = "Select playlist entry:",
        ["默认"] = "Default",
        ["强制"] = "Forced",
        ["依赖"] = "Dependent",
        ["视觉障碍"] = "Visual impaired",
        ["听觉障碍"] = "Hearing impaired",
        ["图像"] = "Image",
        ["外部"] = "External",
        ["视频"] = "Video",
        ["音轨"] = "Audio",
        ["字幕"] = "Subtitle",
        ["没有可用轨道。"] = "No tracks available.",
        ["选择轨道："] = "Select track:",
        ["选择字幕："] = "Select subtitle:",
        ["没有可用字幕。"] = "No subtitles available.",
        ["选择次字幕："] = "Select secondary subtitle:",
        ["选择音轨："] = "Select audio track:",
        ["没有可用音轨。"] = "No audio tracks available.",
        ["选择视频轨："] = "Select video track:",
        ["没有可用视频轨。"] = "No video tracks available.",
        ["没有可用章节。"] = "No chapters available.",
        ["选择章节："] = "Select chapter:",
        ["没有可用版本。"] = "No editions available.",
        ["版本"] = "Edition",
        ["选择版本："] = "Select edition:",
        ["未加载字幕。"] = "No subtitle loaded.",
        ["提取字幕失败：未找到 ffmpeg。"] = "Failed to extract subtitles: ffmpeg not found.",
        ["提取字幕失败。"] = "Failed to extract subtitles.",
        ["选择要跳转的字幕行："] = "Select subtitle line to jump to:",
        ["没有可用音频设备。"] = "No audio devices available.",
        ["选择音频设备："] = "Select audio device:",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "Enable --save-watch-history to jump to recently played files.",
        ["清空历史"] = "Clear history",
        ["选择文件："] = "Select file:",
        ["历史已清空。"] = "History cleared.",
        ["没有找到稍后观看文件。"] = "No watch-later files found.",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "Enable --write-filename-in-watch-later-config to select recent files.",
        ["选择按键绑定："] = "Select key binding:",
        ["查看属性："] = "View properties:",
        ["--no-config 模式下不支持编辑配置文件。"] = "Editing config files is not supported with --no-config.",
        ["次字幕"] = "Secondary subtitle",
        ["字幕行"] = "Subtitle line",
        ["视频轨"] = "Video track",
        ["播放列表"] = "Playlist",
        ["章节"] = "Chapters",
        ["标题"] = "Title",
        ["音频设备"] = "Audio device",
        ["按键绑定"] = "Key bindings",
        ["历史"] = "History",
        ["稍后观看"] = "Watch later",
        ["播放统计"] = "Playback stats",
        ["文件信息"] = "File info",
        ["编辑配置文件"] = "Edit config file",
        ["编辑按键绑定"] = "Edit key bindings",
        ["帮助"] = "Help",
        ["在线文档"] = "Online docs",
    },
    ["ja-JP"] = {
        ["播放列表为空。"] = "プレイリストが空です。",
        ["选择播放列表条目："] = "プレイリストの項目を選択：",
        ["默认"] = "既定",
        ["强制"] = "強制",
        ["依赖"] = "依存",
        ["视觉障碍"] = "視覚障害",
        ["听觉障碍"] = "聴覚障害",
        ["图像"] = "画像",
        ["外部"] = "外部",
        ["视频"] = "ビデオ",
        ["音轨"] = "音声トラック",
        ["字幕"] = "字幕",
        ["没有可用轨道。"] = "トラックがありません。",
        ["选择轨道："] = "トラックを選択：",
        ["选择字幕："] = "字幕を選択：",
        ["没有可用字幕。"] = "字幕がありません。",
        ["选择次字幕："] = "副字幕を選択：",
        ["选择音轨："] = "音声トラックを選択：",
        ["没有可用音轨。"] = "音声トラックがありません。",
        ["选择视频轨："] = "ビデオトラックを選択：",
        ["没有可用视频轨。"] = "ビデオトラックがありません。",
        ["没有可用章节。"] = "チャプターがありません。",
        ["选择章节："] = "チャプターを選択：",
        ["没有可用版本。"] = "エディションがありません。",
        ["版本"] = "エディション",
        ["选择版本："] = "エディションを選択：",
        ["未加载字幕。"] = "字幕が読み込まれていません。",
        ["提取字幕失败：未找到 ffmpeg。"] = "字幕の抽出に失敗：ffmpeg が見つかりません。",
        ["提取字幕失败。"] = "字幕の抽出に失敗しました。",
        ["选择要跳转的字幕行："] = "移動先の字幕行を選択：",
        ["没有可用音频设备。"] = "オーディオデバイスがありません。",
        ["选择音频设备："] = "オーディオデバイスを選択：",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "--save-watch-history を有効にすると最近再生したファイルへ移動できます。",
        ["清空历史"] = "履歴をクリア",
        ["选择文件："] = "ファイルを選択：",
        ["历史已清空。"] = "履歴をクリアしました。",
        ["没有找到稍后观看文件。"] = "後で見るファイルが見つかりません。",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "--write-filename-in-watch-later-config を有効にすると最近のファイルを選択できます。",
        ["选择按键绑定："] = "キーバインドを選択：",
        ["查看属性："] = "プロパティを表示：",
        ["--no-config 模式下不支持编辑配置文件。"] = "--no-config モードでは設定ファイルの編集はできません。",
        ["次字幕"] = "副字幕",
        ["字幕行"] = "字幕行",
        ["视频轨"] = "ビデオトラック",
        ["播放列表"] = "プレイリスト",
        ["章节"] = "チャプター",
        ["标题"] = "タイトル",
        ["音频设备"] = "オーディオデバイス",
        ["按键绑定"] = "キーバインド",
        ["历史"] = "履歴",
        ["稍后观看"] = "後で見る",
        ["播放统计"] = "再生統計",
        ["文件信息"] = "ファイル情報",
        ["编辑配置文件"] = "設定ファイルを編集",
        ["编辑按键绑定"] = "キーバインドを編集",
        ["帮助"] = "ヘルプ",
        ["在线文档"] = "オンラインドキュメント",
    },
    ["ko-KR"] = {
        ["播放列表为空。"] = "재생 목록이 비어 있습니다.",
        ["选择播放列表条目："] = "재생 목록 항목 선택:",
        ["默认"] = "기본",
        ["强制"] = "강제",
        ["依赖"] = "종속",
        ["视觉障碍"] = "시각 장애",
        ["听觉障碍"] = "청각 장애",
        ["图像"] = "이미지",
        ["外部"] = "외부",
        ["视频"] = "비디오",
        ["音轨"] = "오디오 트랙",
        ["字幕"] = "자막",
        ["没有可用轨道。"] = "사용 가능한 트랙이 없습니다.",
        ["选择轨道："] = "트랙 선택:",
        ["选择字幕："] = "자막 선택:",
        ["没有可用字幕。"] = "사용 가능한 자막이 없습니다.",
        ["选择次字幕："] = "보조 자막 선택:",
        ["选择音轨："] = "오디오 트랙 선택:",
        ["没有可用音轨。"] = "사용 가능한 오디오 트랙이 없습니다.",
        ["选择视频轨："] = "비디오 트랙 선택:",
        ["没有可用视频轨。"] = "사용 가능한 비디오 트랙이 없습니다.",
        ["没有可用章节。"] = "사용 가능한 챕터가 없습니다.",
        ["选择章节："] = "챕터 선택:",
        ["没有可用版本。"] = "사용 가능한 에디션이 없습니다.",
        ["版本"] = "에디션",
        ["选择版本："] = "에디션 선택:",
        ["未加载字幕。"] = "자막이 로드되지 않았습니다.",
        ["提取字幕失败：未找到 ffmpeg。"] = "자막 추출 실패: ffmpeg를 찾을 수 없습니다.",
        ["提取字幕失败。"] = "자막 추출에 실패했습니다.",
        ["选择要跳转的字幕行："] = "이동할 자막 줄 선택:",
        ["没有可用音频设备。"] = "사용 가능한 오디오 장치가 없습니다.",
        ["选择音频设备："] = "오디오 장치 선택:",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "--save-watch-history를 활성화하면 최근 재생 파일로 이동할 수 있습니다.",
        ["清空历史"] = "기록 지우기",
        ["选择文件："] = "파일 선택:",
        ["历史已清空。"] = "기록이 지워졌습니다.",
        ["没有找到稍后观看文件。"] = "나중에 볼 파일을 찾을 수 없습니다.",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "--write-filename-in-watch-later-config를 활성화하면 최근 파일을 선택할 수 있습니다.",
        ["选择按键绑定："] = "키 바인딩 선택:",
        ["查看属性："] = "속성 보기:",
        ["--no-config 模式下不支持编辑配置文件。"] = "--no-config 모드에서는 설정 파일을 편집할 수 없습니다.",
        ["次字幕"] = "보조 자막",
        ["字幕行"] = "자막 줄",
        ["视频轨"] = "비디오 트랙",
        ["播放列表"] = "재생 목록",
        ["章节"] = "챕터",
        ["标题"] = "제목",
        ["音频设备"] = "오디오 장치",
        ["按键绑定"] = "키 바인딩",
        ["历史"] = "기록",
        ["稍后观看"] = "나중에 보기",
        ["播放统计"] = "재생 통계",
        ["文件信息"] = "파일 정보",
        ["编辑配置文件"] = "설정 파일 편집",
        ["编辑按键绑定"] = "키 바인딩 편집",
        ["帮助"] = "도움말",
        ["在线文档"] = "온라인 문서",
    },
    ["de-DE"] = {
        ["播放列表为空。"] = "Die Wiedergabeliste ist leer.",
        ["选择播放列表条目："] = "Wiedergabelisteneintrag wählen:",
        ["默认"] = "Standard",
        ["强制"] = "Erzwungen",
        ["依赖"] = "Abhängig",
        ["视觉障碍"] = "Sehbehinderung",
        ["听觉障碍"] = "Hörbehinderung",
        ["图像"] = "Bild",
        ["外部"] = "Extern",
        ["视频"] = "Video",
        ["音轨"] = "Audiospur",
        ["字幕"] = "Untertitel",
        ["没有可用轨道。"] = "Keine Spuren verfügbar.",
        ["选择轨道："] = "Spur wählen:",
        ["选择字幕："] = "Untertitel wählen:",
        ["没有可用字幕。"] = "Keine Untertitel verfügbar.",
        ["选择次字幕："] = "Sekundären Untertitel wählen:",
        ["选择音轨："] = "Audiospur wählen:",
        ["没有可用音轨。"] = "Keine Audiospuren verfügbar.",
        ["选择视频轨："] = "Videospur wählen:",
        ["没有可用视频轨。"] = "Keine Videospuren verfügbar.",
        ["没有可用章节。"] = "Keine Kapitel verfügbar.",
        ["选择章节："] = "Kapitel wählen:",
        ["没有可用版本。"] = "Keine Editionen verfügbar.",
        ["版本"] = "Edition",
        ["选择版本："] = "Edition wählen:",
        ["未加载字幕。"] = "Kein Untertitel geladen.",
        ["提取字幕失败：未找到 ffmpeg。"] = "Untertitel-Extraktion fehlgeschlagen: ffmpeg nicht gefunden.",
        ["提取字幕失败。"] = "Untertitel-Extraktion fehlgeschlagen.",
        ["选择要跳转的字幕行："] = "Untertitelzeile zum Springen wählen:",
        ["没有可用音频设备。"] = "Keine Audiogeräte verfügbar.",
        ["选择音频设备："] = "Audiogerät wählen:",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "Aktivieren Sie --save-watch-history, um zuletzt gespielte Dateien anzuspringen.",
        ["清空历史"] = "Verlauf löschen",
        ["选择文件："] = "Datei wählen:",
        ["历史已清空。"] = "Verlauf gelöscht.",
        ["没有找到稍后观看文件。"] = "Keine Watch-Later-Dateien gefunden.",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "Aktivieren Sie --write-filename-in-watch-later-config, um aktuelle Dateien zu wählen.",
        ["选择按键绑定："] = "Tastenzuordnung wählen:",
        ["查看属性："] = "Eigenschaften anzeigen:",
        ["--no-config 模式下不支持编辑配置文件。"] = "Bearbeiten der Konfigurationsdateien wird mit --no-config nicht unterstützt.",
        ["次字幕"] = "Sekundärer Untertitel",
        ["字幕行"] = "Untertitelzeile",
        ["视频轨"] = "Videospur",
        ["播放列表"] = "Wiedergabeliste",
        ["章节"] = "Kapitel",
        ["标题"] = "Titel",
        ["音频设备"] = "Audiogerät",
        ["按键绑定"] = "Tastenzuordnungen",
        ["历史"] = "Verlauf",
        ["稍后观看"] = "Später ansehen",
        ["播放统计"] = "Wiedergabestatistik",
        ["文件信息"] = "Dateiinfo",
        ["编辑配置文件"] = "Konfigurationsdatei bearbeiten",
        ["编辑按键绑定"] = "Tastenzuordnungen bearbeiten",
        ["帮助"] = "Hilfe",
        ["在线文档"] = "Online-Dokumentation",
    },
    ["fr-FR"] = {
        ["播放列表为空。"] = "La liste de lecture est vide.",
        ["选择播放列表条目："] = "Sélectionner un élément de la liste :",
        ["默认"] = "Par défaut",
        ["强制"] = "Forcé",
        ["依赖"] = "Dépendant",
        ["视觉障碍"] = "Déficience visuelle",
        ["听觉障碍"] = "Déficience auditive",
        ["图像"] = "Image",
        ["外部"] = "Externe",
        ["视频"] = "Vidéo",
        ["音轨"] = "Piste audio",
        ["字幕"] = "Sous-titre",
        ["没有可用轨道。"] = "Aucune piste disponible.",
        ["选择轨道："] = "Sélectionner une piste :",
        ["选择字幕："] = "Sélectionner un sous-titre :",
        ["没有可用字幕。"] = "Aucun sous-titre disponible.",
        ["选择次字幕："] = "Sélectionner le sous-titre secondaire :",
        ["选择音轨："] = "Sélectionner la piste audio :",
        ["没有可用音轨。"] = "Aucune piste audio disponible.",
        ["选择视频轨："] = "Sélectionner la piste vidéo :",
        ["没有可用视频轨。"] = "Aucune piste vidéo disponible.",
        ["没有可用章节。"] = "Aucun chapitre disponible.",
        ["选择章节："] = "Sélectionner un chapitre :",
        ["没有可用版本。"] = "Aucune édition disponible.",
        ["版本"] = "Édition",
        ["选择版本："] = "Sélectionner une édition :",
        ["未加载字幕。"] = "Aucun sous-titre chargé.",
        ["提取字幕失败：未找到 ffmpeg。"] = "Échec de l'extraction : ffmpeg introuvable.",
        ["提取字幕失败。"] = "Échec de l'extraction des sous-titres.",
        ["选择要跳转的字幕行："] = "Sélectionner la ligne de sous-titre :",
        ["没有可用音频设备。"] = "Aucun périphérique audio disponible.",
        ["选择音频设备："] = "Sélectionner le périphérique audio :",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "Activez --save-watch-history pour accéder aux fichiers récents.",
        ["清空历史"] = "Effacer l'historique",
        ["选择文件："] = "Sélectionner un fichier :",
        ["历史已清空。"] = "Historique effacé.",
        ["没有找到稍后观看文件。"] = "Aucun fichier « à regarder plus tard » trouvé.",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "Activez --write-filename-in-watch-later-config pour choisir des fichiers récents.",
        ["选择按键绑定："] = "Sélectionner un raccourci clavier :",
        ["查看属性："] = "Afficher les propriétés :",
        ["--no-config 模式下不支持编辑配置文件。"] = "L'édition des fichiers de configuration n'est pas prise en charge avec --no-config.",
        ["次字幕"] = "Sous-titre secondaire",
        ["字幕行"] = "Ligne de sous-titre",
        ["视频轨"] = "Piste vidéo",
        ["播放列表"] = "Liste de lecture",
        ["章节"] = "Chapitres",
        ["标题"] = "Titre",
        ["音频设备"] = "Périphérique audio",
        ["按键绑定"] = "Raccourcis clavier",
        ["历史"] = "Historique",
        ["稍后观看"] = "À regarder plus tard",
        ["播放统计"] = "Statistiques de lecture",
        ["文件信息"] = "Infos du fichier",
        ["编辑配置文件"] = "Modifier le fichier de configuration",
        ["编辑按键绑定"] = "Modifier les raccourcis clavier",
        ["帮助"] = "Aide",
        ["在线文档"] = "Documentation en ligne",
    },
    ["es-ES"] = {
        ["播放列表为空。"] = "La lista de reproducción está vacía.",
        ["选择播放列表条目："] = "Seleccionar elemento de la lista:",
        ["默认"] = "Predeterminado",
        ["强制"] = "Forzado",
        ["依赖"] = "Dependiente",
        ["视觉障碍"] = "Discapacidad visual",
        ["听觉障碍"] = "Discapacidad auditiva",
        ["图像"] = "Imagen",
        ["外部"] = "Externa",
        ["视频"] = "Video",
        ["音轨"] = "Pista de audio",
        ["字幕"] = "Subtítulo",
        ["没有可用轨道。"] = "No hay pistas disponibles.",
        ["选择轨道："] = "Seleccionar pista:",
        ["选择字幕："] = "Seleccionar subtítulo:",
        ["没有可用字幕。"] = "No hay subtítulos disponibles.",
        ["选择次字幕："] = "Seleccionar subtítulo secundario:",
        ["选择音轨："] = "Seleccionar pista de audio:",
        ["没有可用音轨。"] = "No hay pistas de audio disponibles.",
        ["选择视频轨："] = "Seleccionar pista de video:",
        ["没有可用视频轨。"] = "No hay pistas de video disponibles.",
        ["没有可用章节。"] = "No hay capítulos disponibles.",
        ["选择章节："] = "Seleccionar capítulo:",
        ["没有可用版本。"] = "No hay ediciones disponibles.",
        ["版本"] = "Edición",
        ["选择版本："] = "Seleccionar edición:",
        ["未加载字幕。"] = "No hay subtítulos cargados.",
        ["提取字幕失败：未找到 ffmpeg。"] = "Error al extraer subtítulos: no se encontró ffmpeg.",
        ["提取字幕失败。"] = "Error al extraer los subtítulos.",
        ["选择要跳转的字幕行："] = "Seleccionar línea de subtítulo a la que saltar:",
        ["没有可用音频设备。"] = "No hay dispositivos de audio disponibles.",
        ["选择音频设备："] = "Seleccionar dispositivo de audio:",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "Active --save-watch-history para saltar a archivos recientes.",
        ["清空历史"] = "Borrar historial",
        ["选择文件："] = "Seleccionar archivo:",
        ["历史已清空。"] = "Historial borrado.",
        ["没有找到稍后观看文件。"] = "No se encontraron archivos de « ver más tarde ».",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "Active --write-filename-in-watch-later-config para seleccionar archivos recientes.",
        ["选择按键绑定："] = "Seleccionar atajo de teclado:",
        ["查看属性："] = "Ver propiedades:",
        ["--no-config 模式下不支持编辑配置文件。"] = "No se admite editar archivos de configuración con --no-config.",
        ["次字幕"] = "Subtítulo secundario",
        ["字幕行"] = "Línea de subtítulo",
        ["视频轨"] = "Pista de video",
        ["播放列表"] = "Lista de reproducción",
        ["章节"] = "Capítulos",
        ["标题"] = "Título",
        ["音频设备"] = "Dispositivo de audio",
        ["按键绑定"] = "Atajos de teclado",
        ["历史"] = "Historial",
        ["稍后观看"] = "Ver más tarde",
        ["播放统计"] = "Estadísticas de reproducción",
        ["文件信息"] = "Información del archivo",
        ["编辑配置文件"] = "Editar archivo de configuración",
        ["编辑按键绑定"] = "Editar atajos de teclado",
        ["帮助"] = "Ayuda",
        ["在线文档"] = "Documentación en línea",
    },
    ["ru-RU"] = {
        ["播放列表为空。"] = "Плейлист пуст.",
        ["选择播放列表条目："] = "Выберите элемент плейлиста:",
        ["默认"] = "По умолчанию",
        ["强制"] = "Принудительно",
        ["依赖"] = "Зависимая",
        ["视觉障碍"] = "Нарушение зрения",
        ["听觉障碍"] = "Нарушение слуха",
        ["图像"] = "Изображение",
        ["外部"] = "Внешняя",
        ["视频"] = "Видео",
        ["音轨"] = "Аудиодорожка",
        ["字幕"] = "Субтитры",
        ["没有可用轨道。"] = "Нет доступных дорожек.",
        ["选择轨道："] = "Выберите дорожку:",
        ["选择字幕："] = "Выберите субтитры:",
        ["没有可用字幕。"] = "Нет доступных субтитров.",
        ["选择次字幕："] = "Выберите вторые субтитры:",
        ["选择音轨："] = "Выберите аудиодорожку:",
        ["没有可用音轨。"] = "Нет доступных аудиодорожек.",
        ["选择视频轨："] = "Выберите видеодорожку:",
        ["没有可用视频轨。"] = "Нет доступных видеодорожек.",
        ["没有可用章节。"] = "Нет доступных глав.",
        ["选择章节："] = "Выберите главу:",
        ["没有可用版本。"] = "Нет доступных изданий.",
        ["版本"] = "Издание",
        ["选择版本："] = "Выберите издание:",
        ["未加载字幕。"] = "Субтитры не загружены.",
        ["提取字幕失败：未找到 ffmpeg。"] = "Не удалось извлечь субтитры: ffmpeg не найден.",
        ["提取字幕失败。"] = "Не удалось извлечь субтитры.",
        ["选择要跳转的字幕行："] = "Выберите строку субтитров для перехода:",
        ["没有可用音频设备。"] = "Нет доступных аудиоустройств.",
        ["选择音频设备："] = "Выберите аудиоустройство:",
        ["启用 --save-watch-history 后可跳转最近播放的文件。"] = "Включите --save-watch-history, чтобы переходить к недавним файлам.",
        ["清空历史"] = "Очистить историю",
        ["选择文件："] = "Выберите файл:",
        ["历史已清空。"] = "История очищена.",
        ["没有找到稍后观看文件。"] = "Файлы «смотреть позже» не найдены.",
        ["启用 --write-filename-in-watch-later-config 后可选择最近文件。"] = "Включите --write-filename-in-watch-later-config, чтобы выбирать недавние файлы.",
        ["选择按键绑定："] = "Выберите горячую клавишу:",
        ["查看属性："] = "Просмотр свойств:",
        ["--no-config 模式下不支持编辑配置文件。"] = "Редактирование конфигов не поддерживается в режиме --no-config.",
        ["次字幕"] = "Вторые субтитры",
        ["字幕行"] = "Строка субтитров",
        ["视频轨"] = "Видеодорожка",
        ["播放列表"] = "Плейлист",
        ["章节"] = "Главы",
        ["标题"] = "Заголовок",
        ["音频设备"] = "Аудиоустройство",
        ["按键绑定"] = "Горячие клавиши",
        ["历史"] = "История",
        ["稍后观看"] = "Смотреть позже",
        ["播放统计"] = "Статистика воспроизведения",
        ["文件信息"] = "Информация о файле",
        ["编辑配置文件"] = "Изменить конфиг",
        ["编辑按键绑定"] = "Изменить горячие клавиши",
        ["帮助"] = "Справка",
        ["在线文档"] = "Онлайн-документация",
    },
}

local function _(s)
    local lang = mp.get_property_native("user-data/mpvw/language") or "en-US"
    if lang == "zh-TW" then lang = "zh-CN" end
    local t = select_i18n[lang]
    if t and t[s] then
        return t[s]
    end
    return s
end

local function show_warning(message)
    mp.msg.warn(message)
    if mp.get_property_native("vo-configured") then
        mp.osd_message(message)
    end
end

local function show_error(message)
    mp.msg.error(message)
    if mp.get_property_native("vo-configured") then
        mp.osd_message(message)
    end
end

mp.add_key_binding(nil, "select-playlist", function ()
    local playlist = {}
    local default_item
    local show = mp.get_property_native("osd-playlist-entry")
    local trailing_slash_pattern = mp.get_property("platform") == "windows"
                                   and "[/\\]+$" or "/+$"

    for i, entry in ipairs(mp.get_property_native("playlist")) do
        playlist[i] = entry.title
        if not playlist[i] or show ~= "title" then
            playlist[i] = entry.filename
            if not playlist[i]:find("://") then
                playlist[i] = select(2, utils.split_path(
                    playlist[i]:gsub(trailing_slash_pattern, "")))
            end
        end
        if entry.title and show == "both" then
            playlist[i] = string.format("%s (%s)", entry.title, playlist[i])
        end

        if entry.playing then
            default_item = i
        end
    end

    if #playlist == 0 then
        show_warning(_("播放列表为空。"))
        return
    end

    input.select({
        prompt = _("选择播放列表条目："),
        items = playlist,
        default_item = default_item,
        submit = function (index)
            mp.commandv("playlist-play-index", index - 1)
        end,
    })
end)

local function format_flags(track)
    local flags = ""
    local flag_names = {
        ["default"] = _("默认"),
        ["forced"] = _("强制"),
        ["dependent"] = _("依赖"),
        ["visual-impaired"] = _("视觉障碍"),
        ["hearing-impaired"] = _("听觉障碍"),
        ["image"] = _("图像"),
        ["external"] = _("外部"),
    }

    for _, flag in ipairs({
        "default", "forced", "dependent", "visual-impaired", "hearing-impaired",
        "image", "external"
    }) do
        if track[flag] then
            flags = flags .. (flag_names[flag] or flag) .. " "
        end
    end

    if flags == "" then
        return ""
    end

    return " [" .. flags:sub(1, -2) .. "]"
end

local function format_track(track)
    local bitrate = track["demux-bitrate"] or track["hls-bitrate"]

    return (track.selected and "●" or "○") ..
        (track.title and " " .. track.title or "") ..
        " (" .. (
            (track.lang and track.lang .. " " or "") ..
            (track.codec and track.codec .. " " or "") ..
            (track["demux-w"] and track["demux-w"] .. "x" .. track["demux-h"]
             .. " " or "") ..
            (track["demux-fps"] and not track.image
             and string.format("%.4f", track["demux-fps"]):gsub("%.?0*$", "") ..
             " fps " or "") ..
            (track["demux-channel-count"] and track["demux-channel-count"] ..
             "ch " or "") ..
            (track["codec-profile"] and track.type == "audio"
             and track["codec-profile"] .. " " or "") ..
            (track["demux-samplerate"] and track["demux-samplerate"] / 1000 ..
             " kHz " or "") ..
            (bitrate and string.format("%.0f", bitrate / 1000) ..
             " kbps " or "")
        ):sub(1, -2) .. ")" .. format_flags(track)
end

mp.add_key_binding(nil, "select-track", function ()
    local tracks = {}

    for i, track in ipairs(mp.get_property_native("track-list")) do
        tracks[i] = (track.image and _("图像") or
                     (track.type == "video" and _("视频") or
                      track.type == "audio" and _("音轨") or _("字幕"))) .. ": " ..
                    format_track(track)
    end

    if #tracks == 0 then
        show_warning(_("没有可用轨道。"))
        return
    end

    input.select({
        prompt = _("选择轨道："),
        items = tracks,
        submit = function (id)
            local track = mp.get_property_native("track-list/" .. id - 1)
            if track then
                mp.set_property(track.type, track.selected and "no" or track.id)
            end
        end,
    })
end)

local function select_track(property, type, prompt, warning)
    local tracks = {}
    local items = {}
    local default_item
    local track_id = mp.get_property_native(property)

    for _, track in ipairs(mp.get_property_native("track-list")) do
        if track.type == type then
            tracks[#tracks + 1] = track
            items[#items + 1] = format_track(track)

            if track.id == track_id then
                default_item = #items
            end
        end
    end

    if #items == 0 then
        show_warning(warning)
        return
    end

    input.select({
        prompt = prompt,
        items = items,
        default_item = default_item,
        submit = function (id)
            mp.set_property(property, tracks[id].selected and "no" or tracks[id].id)
        end,
    })
end

mp.add_key_binding(nil, "select-sid", function ()
    select_track("sid", "sub", _("选择字幕："), _("没有可用字幕。"))
end)

mp.add_key_binding(nil, "select-secondary-sid", function ()
    select_track("secondary-sid", "sub", _("选择次字幕："),
                 _("没有可用字幕。"))
end)

mp.add_key_binding(nil, "select-aid", function ()
    select_track("aid", "audio", _("选择音轨："),
                 _("没有可用音轨。"))
end)

mp.add_key_binding(nil, "select-vid", function ()
    select_track("vid", "video", _("选择视频轨："),
                 _("没有可用视频轨。"))
end)

local function format_time(t, duration)
    local fmt = math.max(t, duration) >= 60 * 60 and "%H:%M:%S" or "%M:%S"
    return mp.format_time(t, fmt)
end

mp.add_key_binding(nil, "select-chapter", function ()
    local chapters = {}
    local default_item = mp.get_property_native("chapter")

    if default_item == nil then
        show_warning(_("没有可用章节。"))
        return
    end

    local duration = mp.get_property_native("duration", math.huge)

    for i, chapter in ipairs(mp.get_property_native("chapter-list")) do
        chapters[i] = format_time(chapter.time, duration) .. " " .. chapter.title
    end

    input.select({
        prompt = _("选择章节："),
        items = chapters,
        default_item = default_item > -1 and default_item + 1,
        submit = function (chapter)
            mp.set_property("chapter", chapter - 1)
        end,
    })
end)

mp.add_key_binding(nil, "select-edition", function ()
    local edition_list = mp.get_property_native("edition-list")

    if edition_list == nil or #edition_list < 2 then
        show_warning(_("没有可用版本。"))
        return
    end

    local editions = {}
    local default_item = mp.get_property_native("current-edition")

    for i, edition in ipairs(edition_list) do
        editions[i] = edition.title or (_("版本") .. " " .. edition.id + 1)
    end

    input.select({
        prompt = _("选择版本："),
        items = editions,
        default_item = default_item > -1 and default_item + 1,
        submit = function (edition)
            mp.set_property("edition", edition - 1)
        end,
    })
end)

mp.add_key_binding(nil, "select-subtitle-line", function ()
    local sub = mp.get_property_native("current-tracks/sub")

    if sub == nil then
        show_warning(_("未加载字幕。"))
        return
    end

    if sub.external and sub["external-filename"]:find("^edl://") then
        sub["external-filename"] = sub["external-filename"]:match('https?://.*')
                                   or sub["external-filename"]
    end

    local r = mp.command_native({
        name = "subprocess",
        capture_stdout = true,
        args = sub.external
            and {"ffmpeg", "-loglevel", "error", "-i", sub["external-filename"],
                 "-f", "lrc", "-map_metadata", "-1", "-fflags", "+bitexact", "-"}
            or {"ffmpeg", "-loglevel", "error", "-i", mp.get_property("path"),
                "-map", "s:" .. sub["id"] - 1, "-f", "lrc", "-map_metadata",
                "-1", "-fflags", "+bitexact", "-"}
    })

    if r.error_string == "init" then
        show_error(_("提取字幕失败：未找到 ffmpeg。"))
        return
    elseif r.status ~= 0 then
        show_error(_("提取字幕失败。"))
        return
    end

    local sub_lines = {}
    local sub_times = {}
    local default_item
    local delay = mp.get_property_native("sub-delay")
    local time_pos = mp.get_property_native("time-pos") - delay
    local duration = mp.get_property_native("duration", math.huge)
    local sub_content = {}

    -- Strip HTML and ASS tags and process subtitles
    for line in r.stdout:gmatch("[^\n]+") do
        -- Clean up tags
        local sub_line = line:gsub("<.->", "")                -- Strip HTML tags
                             :gsub("\\h+", " ")               -- Replace '\h' tag
                             :gsub("{[\\=].-}", "")           -- Remove ASS formatting
                             :gsub(".-]", "", 1)              -- Remove time info prefix
                             :gsub("^%s*(.-)%s*$", "%1")      -- Strip whitespace
                             :gsub("^m%s[mbl%s%-%d%.]+$", "") -- Remove graphics code

        if sub.codec == "subrip" or (sub_line ~= "" and sub_line:match("^%s+$") == nil) then
            local sub_time = line:match("%d+") * 60 + line:match(":([%d%.]*)")
            local time_seconds = math.floor(sub_time)
            sub_content[time_seconds] = sub_content[time_seconds] or {}
            sub_content[time_seconds][sub_line] = true
        end
    end

    -- Process all timestamps and content into selectable subtitle list
    for time_seconds, contents in pairs(sub_content) do
        for sub_line in pairs(contents) do
            sub_times[#sub_times + 1] = time_seconds
            sub_lines[#sub_lines + 1] = format_time(time_seconds, duration) .. " " .. sub_line
        end
    end

    -- Generate time -> subtitle mapping
    local time_to_lines = {}
    for i = 1, #sub_times do
        local time = sub_times[i]
        local line = sub_lines[i]

        if not time_to_lines[time] then
            time_to_lines[time] = {}
        end
        table.insert(time_to_lines[time], line)
    end

    -- Sort by timestamp
    local sorted_sub_times = {}
    for i = 1, #sub_times do
        sorted_sub_times[i] = sub_times[i]
    end
    table.sort(sorted_sub_times)

    -- Use a helper table to avoid duplicates
    local added_times = {}

    -- Rebuild sub_lines and sub_times based on the sorted timestamps
    local sorted_sub_lines = {}
    for _, sub_time in ipairs(sorted_sub_times) do
        -- Iterate over all subtitle content for this timestamp
        if not added_times[sub_time] then
            added_times[sub_time] = true
            for _, line in ipairs(time_to_lines[sub_time]) do
                table.insert(sorted_sub_lines, line)
            end
        end
    end

    -- Use the sorted subtitle list
    sub_lines = sorted_sub_lines
    sub_times = sorted_sub_times

    -- Get the default item (last subtitle before current time position)
    for i, sub_time in ipairs(sub_times) do
        if sub_time <= time_pos then
            default_item = i
        end
    end

    input.select({
        prompt = _("选择要跳转的字幕行："),
        items = sub_lines,
        default_item = default_item,
        submit = function (index)
            -- Add an offset to seek to the correct line while paused without a
            -- video track.
            if mp.get_property_native("current-tracks/video/image") ~= false then
                delay = delay + 0.1
            end

            mp.commandv("seek", sub_times[index] + delay, "absolute")
        end,
    })
end)

mp.add_key_binding(nil, "select-audio-device", function ()
    local devices = mp.get_property_native("audio-device-list")
    local items = {}
    -- This is only useful if an --audio-device has been explicitly set,
    -- otherwise its value is just auto and there is no current-audio-device
    -- property.
    local selected_device = mp.get_property("audio-device")
    local default_item

    if #devices == 0 then
        show_warning(_("没有可用音频设备。"))
        return
    end

    for i, device in ipairs(devices) do
        items[i] = device.name .. " (" .. device.description .. ")"

        if device.name == selected_device then
            default_item = i
        end
    end

    input.select({
        prompt = _("选择音频设备："),
        items = items,
        default_item = default_item,
        submit = function (id)
            mp.set_property("audio-device", devices[id].name)
        end,
    })
end)

local function format_history_entry(entry)
    local status
    status, entry.time = pcall(os.date, options.history_date_format, entry.time)

    if not status then
        mp.msg.warn(entry.time)
    end

    local item = "(" .. entry.time .. ") "

    if entry.title then
        return item .. entry.title .. " (" .. entry.path .. ")"
    end

    if entry.path:find("://") then
        return item .. entry.path
    end

    local directory, filename = utils.split_path(entry.path)

    return item .. filename .. " (" .. directory .. ")"
end

mp.add_key_binding(nil, "select-watch-history", function ()
    local history_file_path = mp.command_native(
        {"expand-path", mp.get_property("watch-history-path")})
    local history_file, error_message = io.open(history_file_path)
    if not history_file then
        show_warning(mp.get_property_native("save-watch-history")
                     and error_message
                     or _("启用 --save-watch-history 后可跳转最近播放的文件。"))
        return
    end

    local all_entries = {}
    local line_num = 1
    for line in history_file:lines() do
        local entry = utils.parse_json(line)
        if entry and entry.path then
            all_entries[#all_entries + 1] = entry
        else
            mp.msg.warn(history_file_path .. ": Parse error at line " .. line_num)
        end
        line_num = line_num + 1
    end
    history_file:close()

    local entries = {}
    local items = {}
    local seen = {}

    for i = #all_entries, 1, -1 do
        local entry = all_entries[i]
        if not seen[entry.path] or not options.hide_history_duplicates then
            seen[entry.path] = true
            entries[#entries + 1] = entry
            items[#items + 1] = format_history_entry(entry)
        end
    end

    items[#items+1] = _("清空历史")

    input.select({
        prompt = _("选择文件："),
        items = items,
        submit = function (i)
            if entries[i] then
                mp.commandv("loadfile", entries[i].path)
                return
            end

            error_message = select(2, os.remove(history_file_path))
            if error_message then
                show_error(error_message)
            else
                mp.osd_message(_("历史已清空。"))
            end
        end,
    })
end)

mp.add_key_binding(nil, "select-watch-later", function ()
    local watch_later_dir = mp.get_property("current-watch-later-dir")

    if not watch_later_dir then
        show_warning(_("没有找到稍后观看文件。"))
        return
    end

    local watch_later_files = {}

    for i, file in ipairs(utils.readdir(watch_later_dir, "files") or {}) do
        watch_later_files[i] = watch_later_dir .. "/" .. file
    end

    if #watch_later_files == 0 then
        show_warning(_("没有找到稍后观看文件。"))
        return
    end

    local files = {}
    for _, watch_later_file in pairs(watch_later_files) do
        local file_handle = io.open(watch_later_file)
        if file_handle then
            local line = file_handle:read()
            if line and line ~= "# redirect entry" and line:find("^#") then
                files[#files + 1] = {line:sub(3), utils.file_info(watch_later_file).mtime}
            end
            file_handle:close()
        end
    end

    if #files == 0 then
        show_warning(mp.get_property_native("write-filename-in-watch-later-config")
            and _("没有找到稍后观看文件。")
            or _("启用 --write-filename-in-watch-later-config 后可选择最近文件。"))
        return
    end

    table.sort(files, function (i, j)
        return i[2] > j[2]
    end)

    local items = {}
    for i, file in ipairs(files) do
        items[i] = os.date("(%Y-%m-%d) ", file[2]) .. file[1]
    end

    input.select({
        prompt = _("选择文件："),
        items = items,
        submit = function (i)
            mp.commandv("loadfile", files[i][1])
        end,
    })
end)

mp.add_key_binding(nil, "select-binding", function ()
    local bindings = {}

    for _, binding in pairs(mp.get_property_native("input-bindings")) do
        if binding.priority >= 0 and (
               bindings[binding.key] == nil or
               (bindings[binding.key].is_weak and not binding.is_weak) or
               (binding.is_weak == bindings[binding.key].is_weak and
                binding.priority > bindings[binding.key].priority)
        ) then
            bindings[binding.key] = binding
        end
    end

    local items = {}
    for _, binding in pairs(bindings) do
        if binding.cmd ~= "ignore" then
            items[#items + 1] = binding.key .. " " .. binding.cmd
        end
    end

    table.sort(items)

    input.select({
        prompt = _("选择按键绑定："),
        items = items,
        submit = function (i)
            mp.command(items[i]:gsub("^.- ", ""))
        end,
    })
end)

local properties = {}

local function add_property(property, value)
    value = value or mp.get_property_native(property)

    if type(value) == "table" and next(value) then
        for key, val in pairs(value) do
            add_property(property .. "/" .. key, val)
        end
    else
        properties[#properties + 1] = property .. ": " .. utils.to_string(value)
    end
end

mp.add_key_binding(nil, "show-properties", function ()
    properties = {}

    -- Don't log errors for renamed and removed properties.
    local msg_level_backup = mp.get_property("msg-level")
    mp.set_property("msg-level", msg_level_backup == "" and "cplayer=no"
                                 or msg_level_backup .. ",cplayer=no")

    for _, property in pairs(mp.get_property_native("property-list")) do
        add_property(property)
    end

    mp.set_property("msg-level", msg_level_backup)

    add_property("current-tracks/audio")
    add_property("current-tracks/video")
    add_property("current-tracks/sub")
    add_property("current-tracks/sub2")

    table.sort(properties)

    input.select({
        prompt = _("查看属性："),
        items = properties,
        submit = function (i)
            if mp.get_property_native("vo-configured") then
                mp.commandv("expand-properties", "show-text",
                            (#properties[i] > 100 and
                             "${osd-ass-cc/0}{\\fs9}${osd-ass-cc/1}" or "") ..
                            "$>" .. properties[i], 20000)
            else
                mp.msg.info(properties[i])
            end
        end,
    })
end)

local function system_open(path)
    local platform = mp.get_property("platform")
    local args
    if platform == "windows" then
        args = {"rundll32", "url.dll,FileProtocolHandler", path}
    elseif platform == "darwin" then
        args = {"open", path}
    else
        args = {"gio", "open", path}
    end

    mp.commandv("run", unpack(args))
end

local function edit_config_file(filename)
    if not mp.get_property_bool("config") then
        show_warning(_("--no-config 模式下不支持编辑配置文件。"))
        return
    end

    local path = mp.find_config_file(filename)

    if not path then
        path = mp.command_native({"expand-path", "~~/" .. filename})
        local file_handle, error_message = io.open(path, "w")

        if not file_handle then
            show_error(error_message)
            return
        end

        file_handle:close()
    end

    system_open(path)
end

mp.add_key_binding(nil, "edit-config-file", function ()
    edit_config_file("mpv.conf")
end)

mp.add_key_binding(nil, "edit-input-conf", function ()
    edit_config_file("input.conf")
end)

mp.add_key_binding(nil, "open-docs", function ()
    system_open("https://mpv.io/manual/")
end)

mp.add_key_binding(nil, "menu", function ()
    local sub_track_count = 0
    local audio_track_count = 0
    local video_track_count = 0
    local text_sub_selected = false
    local is_disc = mp.get_property("current-demuxer") == "disc"

    local image_sub_codecs = {["dvd_subtitle"] = true, ["hdmv_pgs_subtitle"] = true}

    for _, track in pairs(mp.get_property_native("track-list")) do
        if track.type == "sub" then
            sub_track_count = sub_track_count + 1

            if track["main-selection"] == 0 and not image_sub_codecs[track.codec] then
                text_sub_selected = true
            end
        elseif track.type == "audio" then
            audio_track_count = audio_track_count + 1
        elseif track.type == "video" then
            video_track_count = video_track_count + 1
        end
    end

    local menu = {
        {_("字幕"), "script-binding select/select-sid", sub_track_count > 0},
        {_("次字幕"), "script-binding select/select-secondary-sid", sub_track_count > 1},
        {_("字幕行"), "script-binding select/select-subtitle-line", text_sub_selected},
        {_("音轨"), "script-message-to dyn_menu audio-menu", audio_track_count > 1},
        {_("视频轨"), "script-binding select/select-vid", video_track_count > 1},
        {_("播放列表"), "script-message-to dyn_menu playlist-menu",
         mp.get_property_native("playlist-count") > 1},
        {_("章节"), "script-binding select/select-chapter", mp.get_property("chapter")},
        {_(is_disc and "标题" or "版本"), "script-binding select/select-edition",
         mp.get_property_native("edition-list/count", 0) > 1},
        {_("音频设备"), "script-binding select/select-audio-device", audio_track_count > 0},
        {_("按键绑定"), "script-binding select/select-binding", true},
        {_("历史"), "script-binding select/select-watch-history", true},
        {_("稍后观看"), "script-binding select/select-watch-later", true},
        {_("播放统计"), "script-binding stats/display-page-1-toggle", true},
        {_("文件信息"), "script-binding stats/display-page-5-toggle",
         mp.get_property("filename")},
        {_("编辑配置文件"), "script-binding select/edit-config-file", true},
        {_("编辑按键绑定"), "script-binding select/edit-input-conf", true},
        {_("帮助"), "script-binding stats/display-page-4-toggle", true},
        {_("在线文档"), "script-binding select/open-docs", true},
    }

    local labels = {}
    local commands = {}

    for _, entry in ipairs(menu) do
        if entry[3] then
            labels[#labels + 1] = entry[1]
            commands[#commands + 1] = entry[2]
        end
    end

    input.select({
        prompt = "",
        items = labels,
        keep_open = true,
        submit = function (i)
            mp.command(commands[i])

            if not commands[i]:find("^script%-binding select/select") then
                input.terminate()
            end
        end,
    })
end)
