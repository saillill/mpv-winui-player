-- Copyright (c) 2023-2024 tsl0922. All rights reserved.
-- SPDX-License-Identifier: GPL-2.0-only

local opts = require('mp.options')
local utils = require('mp.utils')
local msg = require('mp.msg')

-- user options
local o = {
    use_mpv_impl = true,     -- use mpv's menu implementation if available
    uosc_syntax = false,     -- toggle uosc menu syntax support
    escape_title = true,     -- escape & to && in menu title
    max_title_length = 80,   -- limit the title length, set to 0 to disable.
    max_playlist_items = 20, -- limit the playlist items in submenu, set to 0 to disable.
}
opts.read_options(o)

-- ===== 菜单本地化 =====
-- 应用把界面语言写入 user-data/mpvw/language（如 en-US/zh-CN），
-- 这里按语言翻译 input.conf 的 #menu: 标题与动态菜单标题；未收录的键保持原样。
local menu_lang = mp.get_property_native('user-data/mpvw/language') or 'en-US'
-- BEGIN MENU_I18N
local menu_i18n = {
    ['en-US'] = {
        [' 声道'] = ' ch',
        ['0.2 倍'] = '0.2x',
        ['0.5 倍'] = '0.5x',
        ['1.0 倍'] = '1.0x',
        ['1.5 倍'] = '1.5x',
        ['2.0 倍'] = '2.0x',
        ['64.0 倍'] = '64.0x',
        ['[默认]'] = '[default]',
        ['上一帧'] = 'Previous frame',
        ['上一章节'] = 'Previous chapter',
        ['上个文件'] = 'Previous file',
        ['上移'] = 'Move up',
        ['下一帧'] = 'Next frame',
        ['下一章节'] = 'Next chapter',
        ['下个文件'] = 'Next file',
        ['下移'] = 'Move down',
        ['主字幕'] = 'Primary subtitle',
        ['主字幕选项'] = 'Primary subtitle options',
        ['亮度 +1'] = 'Brightness +1',
        ['亮度 -1'] = 'Brightness -1',
        ['伽马 +1'] = 'Gamma +1',
        ['伽马 -1'] = 'Gamma -1',
        ['停止'] = 'Stop',
        ['关闭'] = 'Off',
        ['关闭 VSR'] = 'Disable VSR',
        ['减半'] = 'Halve',
        ['减少字体大小'] = 'Decrease subtitle size',
        ['切换循环播放'] = 'Toggle loop',
        ['切换硬件解码'] = 'Toggle hardware decoding',
        ['切换解码模式'] = 'Toggle decode mode',
        ['切换轨道'] = 'Switch track',
        ['前进 30 秒'] = 'Forward 30s',
        ['前进 5 分钟'] = 'Forward 5min',
        ['前进 5 秒'] = 'Forward 5s',
        ['加载文件...'] = 'Load file...',
        ['原始'] = 'Raw',
        ['去色带'] = 'Deband',
        ['去黑边 +'] = 'Remove borders +',
        ['去黑边 -'] = 'Remove borders -',
        ['反交错'] = 'Deinterlace',
        ['可见性'] = 'Visibility',
        ['右移'] = 'Move right',
        ['后退 30 秒'] = 'Back 30s',
        ['后退 5 分钟'] = 'Back 5min',
        ['后退 5 秒'] = 'Back 5s',
        ['增加'] = 'Increase',
        ['增加字体大小'] = 'Increase subtitle size',
        ['复制 MediaInfo 信息'] = 'Copy MediaInfo',
        ['复制文件路径'] = 'Copy file path',
        ['复制视频元数据'] = 'Copy video metadata',
        ['字幕'] = 'Subtitle',
        ['对比度 +1'] = 'Contrast +1',
        ['对比度 -1'] = 'Contrast -1',
        ['导出播放列表'] = 'Export playlist',
        ['导航'] = 'Navigation',
        ['工具'] = 'Tools',
        ['左移'] = 'Move left',
        ['帧位'] = 'Pan',
        ['常驻显示统计信息'] = 'Persistent stats',
        ['延迟 +0.1'] = 'Delay +0.1',
        ['延迟 -0.1'] = 'Delay -0.1',
        ['强制开启（仅 SDR 片源）'] = 'Force on (SDR source only)',
        ['截屏'] = 'Screenshot',
        ['打乱播放列表'] = 'Shuffle playlist',
        ['打开select分菜单-属性列表'] = 'Select menu: properties',
        ['打开select总菜单'] = 'Select menu',
        ['按键绑定列表'] = 'Key binding list',
        ['播放'] = 'Play',
        ['播放列表'] = 'Playlist',
        ['播放列表为空'] = 'Playlist is empty',
        ['放大 +1%'] = 'Zoom in +1%',
        ['时间码解析模式'] = 'Timecode parse mode',
        ['显示 MediaInfo 信息'] = 'Show MediaInfo',
        ['显示 OSD 时间轴'] = 'Show OSD timeline',
        ['显示控制台'] = 'Show console',
        ['显示统计信息'] = 'Show stats',
        ['显示进度'] = 'Show progress',
        ['暂停'] = 'Pause',
        ['条目'] = 'Item',
        ['查看'] = 'View',
        ['次字幕'] = 'Secondary subtitle',
        ['次字幕选项'] = 'Secondary subtitle options',
        ['比例'] = 'Aspect',
        ['没有可用音轨'] = 'No audio tracks',
        ['清空所有脚本'] = 'Clear all scripts',
        ['清除已记录的属性值'] = 'Clear saved properties',
        ['滤镜与增强'] = 'Filters & Enhance',
        ['版本'] = 'Editions',
        ['着色器'] = 'Shaders',
        ['窗口'] = 'Window',
        ['窗口-无OSD'] = 'Window (no OSD)',
        ['章节'] = 'Chapters',
        ['缩小 -1%'] = 'Zoom out -1%',
        ['缩放'] = 'Zoom',
        ['翻倍'] = 'Double',
        ['自动'] = 'Auto',
        ['自动 ICC 配置'] = 'Auto ICC profile',
        ['自动选择设备'] = 'Auto device',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (SDR source + HDR screen)',
        ['色调 +1'] = 'Hue +1',
        ['色调 -1'] = 'Hue -1',
        ['视频'] = 'Video',
        ['设置/清除 A-B 循环点'] = 'Set/clear A-B loop',
        ['调色'] = 'Color',
        ['轨道'] = 'Tracks',
        ['输出设备'] = 'Output device',
        ['逆时针旋转'] = 'Rotate counterclockwise',
        ['速度'] = 'Speed',
        ['配置文件'] = 'Profiles',
        ['重置'] = 'Reset',
        ['重置 音频与字幕同步'] = 'Reset A/V sync',
        ['降低'] = 'Decrease',
        ['静音'] = 'Mute',
        ['音轨'] = 'Audio',
        ['音量'] = 'Volume',
        ['音频'] = 'Audio',
        ['顺时针旋转'] = 'Rotate clockwise',
        ['饱和度 +1'] = 'Saturation +1',
        ['饱和度 -1'] = 'Saturation -1',
        ['（外部）'] = '(external)',
        ['（强制）'] = '(forced)',
        ['（默认）'] = '(default)',
    },
    ['ja-JP'] = {
        [' 声道'] = ' ch',
        ['0.2 倍'] = '0.2倍',
        ['0.5 倍'] = '0.5倍',
        ['1.0 倍'] = '1.0倍',
        ['1.5 倍'] = '1.5倍',
        ['2.0 倍'] = '2.0倍',
        ['64.0 倍'] = '64.0倍',
        ['[默认]'] = '[既定]',
        ['上一帧'] = '前のフレーム',
        ['上一章节'] = '前のチャプター',
        ['上个文件'] = '前のファイル',
        ['上移'] = '上へ移動',
        ['下一帧'] = '次のフレーム',
        ['下一章节'] = '次のチャプター',
        ['下个文件'] = '次のファイル',
        ['下移'] = '下へ移動',
        ['主字幕'] = '主字幕',
        ['主字幕选项'] = '主字幕オプション',
        ['亮度 +1'] = '明るさ +1',
        ['亮度 -1'] = '明るさ -1',
        ['伽马 +1'] = 'ガンマ +1',
        ['伽马 -1'] = 'ガンマ -1',
        ['停止'] = '停止',
        ['关闭'] = 'オフ',
        ['关闭 VSR'] = 'VSRを無効化',
        ['减半'] = '半分',
        ['减少字体大小'] = '字幕サイズを縮小',
        ['切换循环播放'] = 'ループ切替',
        ['切换硬件解码'] = 'ハードウェアデコード切替',
        ['切换解码模式'] = 'デコードモード切替',
        ['切换轨道'] = 'トラック切替',
        ['前进 30 秒'] = '30秒進む',
        ['前进 5 分钟'] = '5分進む',
        ['前进 5 秒'] = '5秒進む',
        ['加载文件...'] = 'ファイルを読み込む...',
        ['原始'] = '原画',
        ['去色带'] = 'バンド除去',
        ['去黑边 +'] = '黒帯を減らす +',
        ['去黑边 -'] = '黒帯を減らす -',
        ['反交错'] = 'インターレース解除',
        ['可见性'] = '表示',
        ['右移'] = '右へ移動',
        ['后退 30 秒'] = '30秒戻る',
        ['后退 5 分钟'] = '5分戻る',
        ['后退 5 秒'] = '5秒戻る',
        ['增加'] = '上げる',
        ['增加字体大小'] = '字幕サイズを拡大',
        ['复制 MediaInfo 信息'] = 'MediaInfoをコピー',
        ['复制文件路径'] = 'ファイルパスをコピー',
        ['复制视频元数据'] = '動画メタデータをコピー',
        ['字幕'] = '字幕',
        ['对比度 +1'] = 'コントラスト +1',
        ['对比度 -1'] = 'コントラスト -1',
        ['导出播放列表'] = 'プレイリストを書き出し',
        ['导航'] = 'ナビゲーション',
        ['工具'] = 'ツール',
        ['左移'] = '左へ移動',
        ['帧位'] = 'パン',
        ['常驻显示统计信息'] = '統計情報を常時表示',
        ['延迟 +0.1'] = '遅延 +0.1',
        ['延迟 -0.1'] = '遅延 -0.1',
        ['强制开启（仅 SDR 片源）'] = '強制オン（SDRソースのみ）',
        ['截屏'] = 'スクリーンショット',
        ['打乱播放列表'] = 'プレイリストをシャッフル',
        ['打开select分菜单-属性列表'] = 'selectメニュー: プロパティ',
        ['打开select总菜单'] = 'selectメニューを開く',
        ['按键绑定列表'] = 'キーバインド一覧',
        ['播放'] = '再生',
        ['播放列表'] = 'プレイリスト',
        ['播放列表为空'] = 'プレイリストが空です',
        ['放大 +1%'] = '拡大 +1%',
        ['时间码解析模式'] = 'タイムコード解析モード',
        ['显示 MediaInfo 信息'] = 'MediaInfoを表示',
        ['显示 OSD 时间轴'] = 'OSDタイムラインを表示',
        ['显示控制台'] = 'コンソールを表示',
        ['显示统计信息'] = '統計情報を表示',
        ['显示进度'] = '進捗を表示',
        ['暂停'] = '一時停止',
        ['条目'] = '項目',
        ['查看'] = '表示',
        ['次字幕'] = '副字幕',
        ['次字幕选项'] = '副字幕オプション',
        ['比例'] = 'アスペクト比',
        ['没有可用音轨'] = '音声トラックがありません',
        ['清空所有脚本'] = '全スクリプトをクリア',
        ['清除已记录的属性值'] = '保存済みプロパティをクリア',
        ['滤镜与增强'] = 'フィルターと強調',
        ['版本'] = 'エディション',
        ['着色器'] = 'シェーダー',
        ['窗口'] = 'ウィンドウ',
        ['窗口-无OSD'] = 'ウィンドウ（OSDなし）',
        ['章节'] = 'チャプター',
        ['缩小 -1%'] = '縮小 -1%',
        ['缩放'] = 'ズーム',
        ['翻倍'] = '2倍',
        ['自动'] = '自動',
        ['自动 ICC 配置'] = 'ICCプロファイルを自動設定',
        ['自动选择设备'] = 'デバイスを自動選択',
        ['自动（SDR 片源 + 屏幕 HDR）'] = '自動（SDRソース＋HDR画面）',
        ['色调 +1'] = '色相 +1',
        ['色调 -1'] = '色相 -1',
        ['视频'] = 'ビデオ',
        ['设置/清除 A-B 循环点'] = 'A-Bループ設定/解除',
        ['调色'] = '色調整',
        ['轨道'] = 'トラック',
        ['输出设备'] = '出力デバイス',
        ['逆时针旋转'] = '反時計回りに回転',
        ['速度'] = '速度',
        ['配置文件'] = 'プロファイル',
        ['重置'] = 'リセット',
        ['重置 音频与字幕同步'] = 'A/V同期をリセット',
        ['降低'] = '下げる',
        ['静音'] = 'ミュート',
        ['音轨'] = '音声トラック',
        ['音量'] = '音量',
        ['音频'] = '音声',
        ['顺时针旋转'] = '時計回りに回転',
        ['饱和度 +1'] = '彩度 +1',
        ['饱和度 -1'] = '彩度 -1',
        ['（外部）'] = '（外部）',
        ['（强制）'] = '（強制）',
        ['（默认）'] = '（既定）',
    },
    ['ko-KR'] = {
        [' 声道'] = 'ch',
        ['0.2 倍'] = '0.2배',
        ['0.5 倍'] = '0.5배',
        ['1.0 倍'] = '1.0배',
        ['1.5 倍'] = '1.5배',
        ['2.0 倍'] = '2.0배',
        ['64.0 倍'] = '64.0배',
        ['[默认]'] = '[기본]',
        ['上一帧'] = '이전 프레임',
        ['上一章节'] = '이전 챕터',
        ['上个文件'] = '이전 파일',
        ['上移'] = '위로 이동',
        ['下一帧'] = '다음 프레임',
        ['下一章节'] = '다음 챕터',
        ['下个文件'] = '다음 파일',
        ['下移'] = '아래로 이동',
        ['主字幕'] = '기본 자막',
        ['主字幕选项'] = '기본 자막 옵션',
        ['亮度 +1'] = '밝기 +1',
        ['亮度 -1'] = '밝기 -1',
        ['伽马 +1'] = '감마 +1',
        ['伽马 -1'] = '감마 -1',
        ['停止'] = '정지',
        ['关闭'] = '끄기',
        ['关闭 VSR'] = 'VSR 비활성화',
        ['减半'] = '절반',
        ['减少字体大小'] = '자막 크기 줄이기',
        ['切换循环播放'] = '반복 전환',
        ['切换硬件解码'] = '하드웨어 디코딩 전환',
        ['切换解码模式'] = '디코딩 모드 전환',
        ['切换轨道'] = '트랙 전환',
        ['前进 30 秒'] = '30초 앞으로',
        ['前进 5 分钟'] = '5분 앞으로',
        ['前进 5 秒'] = '5초 앞으로',
        ['加载文件...'] = '파일 불러오기...',
        ['原始'] = '원본',
        ['去色带'] = '밴딩 제거',
        ['去黑边 +'] = '테두리 제거 +',
        ['去黑边 -'] = '테두리 제거 -',
        ['反交错'] = '디인터레이스',
        ['可见性'] = '표시',
        ['右移'] = '오른쪽으로 이동',
        ['后退 30 秒'] = '30초 뒤로',
        ['后退 5 分钟'] = '5분 뒤로',
        ['后退 5 秒'] = '5초 뒤로',
        ['增加'] = '높이기',
        ['增加字体大小'] = '자막 크기 늘리기',
        ['复制 MediaInfo 信息'] = 'MediaInfo 복사',
        ['复制文件路径'] = '파일 경로 복사',
        ['复制视频元数据'] = '동영상 메타데이터 복사',
        ['字幕'] = '자막',
        ['对比度 +1'] = '대비 +1',
        ['对比度 -1'] = '대비 -1',
        ['导出播放列表'] = '재생 목록 내보내기',
        ['导航'] = '탐색',
        ['工具'] = '도구',
        ['左移'] = '왼쪽으로 이동',
        ['帧位'] = '이동',
        ['常驻显示统计信息'] = '통계 상시 표시',
        ['延迟 +0.1'] = '지연 +0.1',
        ['延迟 -0.1'] = '지연 -0.1',
        ['强制开启（仅 SDR 片源）'] = '강제 켜기(SDR 소스만)',
        ['截屏'] = '스크린샷',
        ['打乱播放列表'] = '재생 목록 셔플',
        ['打开select分菜单-属性列表'] = 'select 메뉴: 속성',
        ['打开select总菜单'] = 'select 메뉴 열기',
        ['按键绑定列表'] = '키 바인딩 목록',
        ['播放'] = '재생',
        ['播放列表'] = '재생 목록',
        ['播放列表为空'] = '재생 목록이 비어 있음',
        ['放大 +1%'] = '확대 +1%',
        ['时间码解析模式'] = '타임코드 파싱 모드',
        ['显示 MediaInfo 信息'] = 'MediaInfo 표시',
        ['显示 OSD 时间轴'] = 'OSD 타임라인 표시',
        ['显示控制台'] = '콘솔 표시',
        ['显示统计信息'] = '통계 표시',
        ['显示进度'] = '진행률 표시',
        ['暂停'] = '일시정지',
        ['条目'] = '항목',
        ['查看'] = '보기',
        ['次字幕'] = '보조 자막',
        ['次字幕选项'] = '보조 자막 옵션',
        ['比例'] = '화면 비율',
        ['没有可用音轨'] = '오디오 트랙 없음',
        ['清空所有脚本'] = '모든 스크립트 지우기',
        ['清除已记录的属性值'] = '저장된 속성 지우기',
        ['滤镜与增强'] = '필터 및 향상',
        ['版本'] = '에디션',
        ['着色器'] = '셰이더',
        ['窗口'] = '창',
        ['窗口-无OSD'] = '창(OSD 없음)',
        ['章节'] = '챕터',
        ['缩小 -1%'] = '축소 -1%',
        ['缩放'] = '확대',
        ['翻倍'] = '두 배',
        ['自动'] = '자동',
        ['自动 ICC 配置'] = '자동 ICC 프로필',
        ['自动选择设备'] = '장치 자동 선택',
        ['自动（SDR 片源 + 屏幕 HDR）'] = '자동(SDR 소스 + HDR 화면)',
        ['色调 +1'] = '색조 +1',
        ['色调 -1'] = '색조 -1',
        ['视频'] = '비디오',
        ['设置/清除 A-B 循环点'] = 'A-B 반복 설정/해제',
        ['调色'] = '색상',
        ['轨道'] = '트랙',
        ['输出设备'] = '출력 장치',
        ['逆时针旋转'] = '반시계 방향 회전',
        ['速度'] = '속도',
        ['配置文件'] = '프로필',
        ['重置'] = '초기화',
        ['重置 音频与字幕同步'] = 'A/V 동기화 초기화',
        ['降低'] = '낮추기',
        ['静音'] = '음소거',
        ['音轨'] = '오디오 트랙',
        ['音量'] = '볼륨',
        ['音频'] = '오디오',
        ['顺时针旋转'] = '시계 방향 회전',
        ['饱和度 +1'] = '채도 +1',
        ['饱和度 -1'] = '채도 -1',
        ['（外部）'] = '(외부)',
        ['（强制）'] = '(강제)',
        ['（默认）'] = '(기본)',
    },
    ['de-DE'] = {
        [' 声道'] = ' Kanäle',
        ['0.2 倍'] = '0,2x',
        ['0.5 倍'] = '0,5x',
        ['1.0 倍'] = '1,0x',
        ['1.5 倍'] = '1,5x',
        ['2.0 倍'] = '2,0x',
        ['64.0 倍'] = '64,0x',
        ['[默认]'] = '[Standard]',
        ['上一帧'] = 'Vorheriger Frame',
        ['上一章节'] = 'Vorheriges Kapitel',
        ['上个文件'] = 'Vorherige Datei',
        ['上移'] = 'Nach oben',
        ['下一帧'] = 'Nächster Frame',
        ['下一章节'] = 'Nächstes Kapitel',
        ['下个文件'] = 'Nächste Datei',
        ['下移'] = 'Nach unten',
        ['主字幕'] = 'Primärer Untertitel',
        ['主字幕选项'] = 'Optionen für primäre Untertitel',
        ['亮度 +1'] = 'Helligkeit +1',
        ['亮度 -1'] = 'Helligkeit -1',
        ['伽马 +1'] = 'Gamma +1',
        ['伽马 -1'] = 'Gamma -1',
        ['停止'] = 'Stopp',
        ['关闭'] = 'Aus',
        ['关闭 VSR'] = 'VSR deaktivieren',
        ['减半'] = 'Halbieren',
        ['减少字体大小'] = 'Untertitelgröße verkleinern',
        ['切换循环播放'] = 'Schleife umschalten',
        ['切换硬件解码'] = 'Hardware-Dekodierung umschalten',
        ['切换解码模式'] = 'Dekodiermodus umschalten',
        ['切换轨道'] = 'Spur wechseln',
        ['前进 30 秒'] = '30 s vor',
        ['前进 5 分钟'] = '5 min vor',
        ['前进 5 秒'] = '5 s vor',
        ['加载文件...'] = 'Datei laden...',
        ['原始'] = 'Roh',
        ['去色带'] = 'Debanding',
        ['去黑边 +'] = 'Ränder entfernen +',
        ['去黑边 -'] = 'Ränder entfernen -',
        ['反交错'] = 'Deinterlacing',
        ['可见性'] = 'Sichtbarkeit',
        ['右移'] = 'Nach rechts',
        ['后退 30 秒'] = '30 s zurück',
        ['后退 5 分钟'] = '5 min zurück',
        ['后退 5 秒'] = '5 s zurück',
        ['增加'] = 'Erhöhen',
        ['增加字体大小'] = 'Untertitelgröße vergrößern',
        ['复制 MediaInfo 信息'] = 'MediaInfo kopieren',
        ['复制文件路径'] = 'Dateipfad kopieren',
        ['复制视频元数据'] = 'Videometadaten kopieren',
        ['字幕'] = 'Untertitel',
        ['对比度 +1'] = 'Kontrast +1',
        ['对比度 -1'] = 'Kontrast -1',
        ['导出播放列表'] = 'Wiedergabeliste exportieren',
        ['导航'] = 'Navigation',
        ['工具'] = 'Werkzeuge',
        ['左移'] = 'Nach links',
        ['帧位'] = 'Schwenken',
        ['常驻显示统计信息'] = 'Dauerhafte Statistiken',
        ['延迟 +0.1'] = 'Verzögerung +0.1',
        ['延迟 -0.1'] = 'Verzögerung -0.1',
        ['强制开启（仅 SDR 片源）'] = 'Erzwingen (nur SDR-Quelle)',
        ['截屏'] = 'Screenshot',
        ['打乱播放列表'] = 'Wiedergabeliste mischen',
        ['打开select分菜单-属性列表'] = 'Select-Menü: Eigenschaften',
        ['打开select总菜单'] = 'Select-Menü öffnen',
        ['按键绑定列表'] = 'Tastenzuordnungsliste',
        ['播放'] = 'Wiedergabe',
        ['播放列表'] = 'Wiedergabeliste',
        ['播放列表为空'] = 'Wiedergabeliste ist leer',
        ['放大 +1%'] = 'Vergrößern +1%',
        ['时间码解析模式'] = 'Timecode-Analysemodus',
        ['显示 MediaInfo 信息'] = 'MediaInfo anzeigen',
        ['显示 OSD 时间轴'] = 'OSD-Zeitleiste anzeigen',
        ['显示控制台'] = 'Konsole anzeigen',
        ['显示统计信息'] = 'Statistiken anzeigen',
        ['显示进度'] = 'Fortschritt anzeigen',
        ['暂停'] = 'Pause',
        ['条目'] = 'Eintrag',
        ['查看'] = 'Ansicht',
        ['次字幕'] = 'Sekundärer Untertitel',
        ['次字幕选项'] = 'Optionen für sekundäre Untertitel',
        ['比例'] = 'Seitenverhältnis',
        ['没有可用音轨'] = 'Keine Audiospuren',
        ['清空所有脚本'] = 'Alle Skripte löschen',
        ['清除已记录的属性值'] = 'Gespeicherte Eigenschaften löschen',
        ['滤镜与增强'] = 'Filter & Verbessern',
        ['版本'] = 'Editionen',
        ['着色器'] = 'Shader',
        ['窗口'] = 'Fenster',
        ['窗口-无OSD'] = 'Fenster (ohne OSD)',
        ['章节'] = 'Kapitel',
        ['缩小 -1%'] = 'Verkleinern -1%',
        ['缩放'] = 'Zoom',
        ['翻倍'] = 'Verdoppeln',
        ['自动'] = 'Automatisch',
        ['自动 ICC 配置'] = 'Automatisches ICC-Profil',
        ['自动选择设备'] = 'Automatisches Gerät',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Automatisch (SDR-Quelle + HDR-Bildschirm)',
        ['色调 +1'] = 'Farbton +1',
        ['色调 -1'] = 'Farbton -1',
        ['视频'] = 'Video',
        ['设置/清除 A-B 循环点'] = 'A-B-Schleife setzen/löschen',
        ['调色'] = 'Farbe',
        ['轨道'] = 'Spuren',
        ['输出设备'] = 'Ausgabegerät',
        ['逆时针旋转'] = 'Gegen den Uhrzeigersinn drehen',
        ['速度'] = 'Geschwindigkeit',
        ['配置文件'] = 'Profile',
        ['重置'] = 'Zurücksetzen',
        ['重置 音频与字幕同步'] = 'A/V-Synchronisation zurücksetzen',
        ['降低'] = 'Verringern',
        ['静音'] = 'Stumm',
        ['音轨'] = 'Audiospur',
        ['音量'] = 'Lautstärke',
        ['音频'] = 'Audio',
        ['顺时针旋转'] = 'Im Uhrzeigersinn drehen',
        ['饱和度 +1'] = 'Sättigung +1',
        ['饱和度 -1'] = 'Sättigung -1',
        ['（外部）'] = '(extern)',
        ['（强制）'] = '(erzwungen)',
        ['（默认）'] = '(Standard)',
    },
    ['fr-FR'] = {
        [' 声道'] = ' canaux',
        ['0.2 倍'] = '0,2x',
        ['0.5 倍'] = '0,5x',
        ['1.0 倍'] = '1,0x',
        ['1.5 倍'] = '1,5x',
        ['2.0 倍'] = '2,0x',
        ['64.0 倍'] = '64,0x',
        ['[默认]'] = '[par défaut]',
        ['上一帧'] = 'Image précédente',
        ['上一章节'] = 'Chapitre précédent',
        ['上个文件'] = 'Fichier précédent',
        ['上移'] = 'Déplacer vers le haut',
        ['下一帧'] = 'Image suivante',
        ['下一章节'] = 'Chapitre suivant',
        ['下个文件'] = 'Fichier suivant',
        ['下移'] = 'Déplacer vers le bas',
        ['主字幕'] = 'Sous-titre principal',
        ['主字幕选项'] = 'Options du sous-titre principal',
        ['亮度 +1'] = 'Luminosité +1',
        ['亮度 -1'] = 'Luminosité -1',
        ['伽马 +1'] = 'Gamma +1',
        ['伽马 -1'] = 'Gamma -1',
        ['停止'] = 'Arrêter',
        ['关闭'] = 'Désactivé',
        ['关闭 VSR'] = 'Désactiver VSR',
        ['减半'] = 'Diviser par deux',
        ['减少字体大小'] = 'Réduire la taille des sous-titres',
        ['切换循环播放'] = 'Basculer la boucle',
        ['切换硬件解码'] = 'Basculer le décodage matériel',
        ['切换解码模式'] = 'Basculer le mode de décodage',
        ['切换轨道'] = 'Changer de piste',
        ['前进 30 秒'] = 'Avancer de 30 s',
        ['前进 5 分钟'] = 'Avancer de 5 min',
        ['前进 5 秒'] = 'Avancer de 5 s',
        ['加载文件...'] = 'Charger un fichier...',
        ['原始'] = 'Brut',
        ['去色带'] = 'Débanding',
        ['去黑边 +'] = 'Réduire les bordures +',
        ['去黑边 -'] = 'Réduire les bordures -',
        ['反交错'] = 'Désentrelacement',
        ['可见性'] = 'Visibilité',
        ['右移'] = 'Déplacer vers la droite',
        ['后退 30 秒'] = 'Reculer de 30 s',
        ['后退 5 分钟'] = 'Reculer de 5 min',
        ['后退 5 秒'] = 'Reculer de 5 s',
        ['增加'] = 'Augmenter',
        ['增加字体大小'] = 'Augmenter la taille des sous-titres',
        ['复制 MediaInfo 信息'] = 'Copier MediaInfo',
        ['复制文件路径'] = 'Copier le chemin du fichier',
        ['复制视频元数据'] = 'Copier les métadonnées vidéo',
        ['字幕'] = 'Sous-titre',
        ['对比度 +1'] = 'Contraste +1',
        ['对比度 -1'] = 'Contraste -1',
        ['导出播放列表'] = 'Exporter la liste de lecture',
        ['导航'] = 'Navigation',
        ['工具'] = 'Outils',
        ['左移'] = 'Déplacer vers la gauche',
        ['帧位'] = 'Panoramique',
        ['常驻显示统计信息'] = 'Statistiques persistantes',
        ['延迟 +0.1'] = 'Délai +0.1',
        ['延迟 -0.1'] = 'Délai -0.1',
        ['强制开启（仅 SDR 片源）'] = 'Forcer (source SDR uniquement)',
        ['截屏'] = 'Capture d\'écran',
        ['打乱播放列表'] = 'Mélanger la liste de lecture',
        ['打开select分菜单-属性列表'] = 'Menu select : propriétés',
        ['打开select总菜单'] = 'Ouvrir le menu select',
        ['按键绑定列表'] = 'Liste des raccourcis clavier',
        ['播放'] = 'Lecture',
        ['播放列表'] = 'Liste de lecture',
        ['播放列表为空'] = 'La liste de lecture est vide',
        ['放大 +1%'] = 'Zoom avant +1%',
        ['时间码解析模式'] = 'Mode d\'analyse du timecode',
        ['显示 MediaInfo 信息'] = 'Afficher MediaInfo',
        ['显示 OSD 时间轴'] = 'Afficher la timeline OSD',
        ['显示控制台'] = 'Afficher la console',
        ['显示统计信息'] = 'Afficher les statistiques',
        ['显示进度'] = 'Afficher la progression',
        ['暂停'] = 'Pause',
        ['条目'] = 'Élément',
        ['查看'] = 'Affichage',
        ['次字幕'] = 'Sous-titre secondaire',
        ['次字幕选项'] = 'Options du sous-titre secondaire',
        ['比例'] = 'Format',
        ['没有可用音轨'] = 'Aucune piste audio',
        ['清空所有脚本'] = 'Effacer tous les scripts',
        ['清除已记录的属性值'] = 'Effacer les propriétés enregistrées',
        ['滤镜与增强'] = 'Filtres et amélioration',
        ['版本'] = 'Éditions',
        ['着色器'] = 'Shaders',
        ['窗口'] = 'Fenêtre',
        ['窗口-无OSD'] = 'Fenêtre (sans OSD)',
        ['章节'] = 'Chapitres',
        ['缩小 -1%'] = 'Zoom arrière -1%',
        ['缩放'] = 'Zoom',
        ['翻倍'] = 'Doubler',
        ['自动'] = 'Auto',
        ['自动 ICC 配置'] = 'Profil ICC automatique',
        ['自动选择设备'] = 'Périphérique automatique',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (source SDR + écran HDR)',
        ['色调 +1'] = 'Teinte +1',
        ['色调 -1'] = 'Teinte -1',
        ['视频'] = 'Vidéo',
        ['设置/清除 A-B 循环点'] = 'Définir/effacer la boucle A-B',
        ['调色'] = 'Couleur',
        ['轨道'] = 'Pistes',
        ['输出设备'] = 'Périphérique de sortie',
        ['逆时针旋转'] = 'Pivoter dans le sens antihoraire',
        ['速度'] = 'Vitesse',
        ['配置文件'] = 'Profils',
        ['重置'] = 'Réinitialiser',
        ['重置 音频与字幕同步'] = 'Réinitialiser la synchro A/V',
        ['降低'] = 'Diminuer',
        ['静音'] = 'Couper le son',
        ['音轨'] = 'Piste audio',
        ['音量'] = 'Volume',
        ['音频'] = 'Audio',
        ['顺时针旋转'] = 'Pivoter dans le sens horaire',
        ['饱和度 +1'] = 'Saturation +1',
        ['饱和度 -1'] = 'Saturation -1',
        ['（外部）'] = '(externe)',
        ['（强制）'] = '(forcé)',
        ['（默认）'] = '(par défaut)',
    },
    ['es-ES'] = {
        [' 声道'] = ' canales',
        ['0.2 倍'] = '0,2x',
        ['0.5 倍'] = '0,5x',
        ['1.0 倍'] = '1,0x',
        ['1.5 倍'] = '1,5x',
        ['2.0 倍'] = '2,0x',
        ['64.0 倍'] = '64,0x',
        ['[默认]'] = '[predeterminada]',
        ['上一帧'] = 'Fotograma anterior',
        ['上一章节'] = 'Capítulo anterior',
        ['上个文件'] = 'Archivo anterior',
        ['上移'] = 'Mover arriba',
        ['下一帧'] = 'Fotograma siguiente',
        ['下一章节'] = 'Capítulo siguiente',
        ['下个文件'] = 'Archivo siguiente',
        ['下移'] = 'Mover abajo',
        ['主字幕'] = 'Subtítulo principal',
        ['主字幕选项'] = 'Opciones de subtítulo principal',
        ['亮度 +1'] = 'Brillo +1',
        ['亮度 -1'] = 'Brillo -1',
        ['伽马 +1'] = 'Gamma +1',
        ['伽马 -1'] = 'Gamma -1',
        ['停止'] = 'Detener',
        ['关闭'] = 'Apagado',
        ['关闭 VSR'] = 'Desactivar VSR',
        ['减半'] = 'Reducir a la mitad',
        ['减少字体大小'] = 'Reducir tamaño de subtítulos',
        ['切换循环播放'] = 'Cambiar bucle',
        ['切换硬件解码'] = 'Cambiar decodificación por hardware',
        ['切换解码模式'] = 'Cambiar modo de decodificación',
        ['切换轨道'] = 'Cambiar de pista',
        ['前进 30 秒'] = 'Adelante 30 s',
        ['前进 5 分钟'] = 'Adelante 5 min',
        ['前进 5 秒'] = 'Adelante 5 s',
        ['加载文件...'] = 'Cargar archivo...',
        ['原始'] = 'Original',
        ['去色带'] = 'Debanding',
        ['去黑边 +'] = 'Quitar bordes +',
        ['去黑边 -'] = 'Quitar bordes -',
        ['反交错'] = 'Desentrelazado',
        ['可见性'] = 'Visibilidad',
        ['右移'] = 'Mover a la derecha',
        ['后退 30 秒'] = 'Atrás 30 s',
        ['后退 5 分钟'] = 'Atrás 5 min',
        ['后退 5 秒'] = 'Atrás 5 s',
        ['增加'] = 'Subir',
        ['增加字体大小'] = 'Aumentar tamaño de subtítulos',
        ['复制 MediaInfo 信息'] = 'Copiar MediaInfo',
        ['复制文件路径'] = 'Copiar ruta del archivo',
        ['复制视频元数据'] = 'Copiar metadatos de video',
        ['字幕'] = 'Subtítulo',
        ['对比度 +1'] = 'Contraste +1',
        ['对比度 -1'] = 'Contraste -1',
        ['导出播放列表'] = 'Exportar lista de reproducción',
        ['导航'] = 'Navegación',
        ['工具'] = 'Herramientas',
        ['左移'] = 'Mover a la izquierda',
        ['帧位'] = 'Panorámica',
        ['常驻显示统计信息'] = 'Estadísticas persistentes',
        ['延迟 +0.1'] = 'Retardo +0.1',
        ['延迟 -0.1'] = 'Retardo -0.1',
        ['强制开启（仅 SDR 片源）'] = 'Forzar (solo fuente SDR)',
        ['截屏'] = 'Captura de pantalla',
        ['打乱播放列表'] = 'Mezclar lista de reproducción',
        ['打开select分菜单-属性列表'] = 'Menú select: propiedades',
        ['打开select总菜单'] = 'Abrir menú select',
        ['按键绑定列表'] = 'Lista de atajos de teclado',
        ['播放'] = 'Reproducción',
        ['播放列表'] = 'Lista de reproducción',
        ['播放列表为空'] = 'La lista está vacía',
        ['放大 +1%'] = 'Acercar +1%',
        ['时间码解析模式'] = 'Modo de análisis de código de tiempo',
        ['显示 MediaInfo 信息'] = 'Mostrar MediaInfo',
        ['显示 OSD 时间轴'] = 'Mostrar línea de tiempo OSD',
        ['显示控制台'] = 'Mostrar consola',
        ['显示统计信息'] = 'Mostrar estadísticas',
        ['显示进度'] = 'Mostrar progreso',
        ['暂停'] = 'Pausa',
        ['条目'] = 'Elemento',
        ['查看'] = 'Ver',
        ['次字幕'] = 'Subtítulo secundario',
        ['次字幕选项'] = 'Opciones de subtítulo secundario',
        ['比例'] = 'Relación de aspecto',
        ['没有可用音轨'] = 'Sin pistas de audio',
        ['清空所有脚本'] = 'Limpiar todos los scripts',
        ['清除已记录的属性值'] = 'Limpiar propiedades guardadas',
        ['滤镜与增强'] = 'Filtros y mejora',
        ['版本'] = 'Ediciones',
        ['着色器'] = 'Shaders',
        ['窗口'] = 'Ventana',
        ['窗口-无OSD'] = 'Ventana (sin OSD)',
        ['章节'] = 'Capítulos',
        ['缩小 -1%'] = 'Alejar -1%',
        ['缩放'] = 'Zoom',
        ['翻倍'] = 'Duplicar',
        ['自动'] = 'Auto',
        ['自动 ICC 配置'] = 'Perfil ICC automático',
        ['自动选择设备'] = 'Dispositivo automático',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Auto (fuente SDR + pantalla HDR)',
        ['色调 +1'] = 'Tono +1',
        ['色调 -1'] = 'Tono -1',
        ['视频'] = 'Video',
        ['设置/清除 A-B 循环点'] = 'Establecer/limpiar bucle A-B',
        ['调色'] = 'Color',
        ['轨道'] = 'Pistas',
        ['输出设备'] = 'Dispositivo de salida',
        ['逆时针旋转'] = 'Girar en sentido antihorario',
        ['速度'] = 'Velocidad',
        ['配置文件'] = 'Perfiles',
        ['重置'] = 'Restablecer',
        ['重置 音频与字幕同步'] = 'Restablecer sincronización A/V',
        ['降低'] = 'Bajar',
        ['静音'] = 'Silenciar',
        ['音轨'] = 'Pista de audio',
        ['音量'] = 'Volumen',
        ['音频'] = 'Audio',
        ['顺时针旋转'] = 'Girar en sentido horario',
        ['饱和度 +1'] = 'Saturación +1',
        ['饱和度 -1'] = 'Saturación -1',
        ['（外部）'] = '(externa)',
        ['（强制）'] = '(forzada)',
        ['（默认）'] = '(predeterminada)',
    },
    ['ru-RU'] = {
        [' 声道'] = ' канал.',
        ['0.2 倍'] = '0,2x',
        ['0.5 倍'] = '0,5x',
        ['1.0 倍'] = '1,0x',
        ['1.5 倍'] = '1,5x',
        ['2.0 倍'] = '2,0x',
        ['64.0 倍'] = '64,0x',
        ['[默认]'] = '[по умолчанию]',
        ['上一帧'] = 'Предыдущий кадр',
        ['上一章节'] = 'Предыдущая глава',
        ['上个文件'] = 'Предыдущий файл',
        ['上移'] = 'Переместить вверх',
        ['下一帧'] = 'Следующий кадр',
        ['下一章节'] = 'Следующая глава',
        ['下个文件'] = 'Следующий файл',
        ['下移'] = 'Переместить вниз',
        ['主字幕'] = 'Основные субтитры',
        ['主字幕选项'] = 'Параметры основных субтитров',
        ['亮度 +1'] = 'Яркость +1',
        ['亮度 -1'] = 'Яркость -1',
        ['伽马 +1'] = 'Гамма +1',
        ['伽马 -1'] = 'Гамма -1',
        ['停止'] = 'Стоп',
        ['关闭'] = 'Выкл',
        ['关闭 VSR'] = 'Отключить VSR',
        ['减半'] = 'Вдвое меньше',
        ['减少字体大小'] = 'Уменьшить размер субтитров',
        ['切换循环播放'] = 'Переключить зацикливание',
        ['切换硬件解码'] = 'Переключить аппаратное декодирование',
        ['切换解码模式'] = 'Переключить режим декодирования',
        ['切换轨道'] = 'Переключить дорожку',
        ['前进 30 秒'] = 'Вперёд на 30 с',
        ['前进 5 分钟'] = 'Вперёд на 5 мин',
        ['前进 5 秒'] = 'Вперёд на 5 с',
        ['加载文件...'] = 'Загрузить файл...',
        ['原始'] = 'Исходный',
        ['去色带'] = 'Дебэндинг',
        ['去黑边 +'] = 'Убрать поля +',
        ['去黑边 -'] = 'Убрать поля -',
        ['反交错'] = 'Деинтерлейс',
        ['可见性'] = 'Видимость',
        ['右移'] = 'Переместить вправо',
        ['后退 30 秒'] = 'Назад на 30 с',
        ['后退 5 分钟'] = 'Назад на 5 мин',
        ['后退 5 秒'] = 'Назад на 5 с',
        ['增加'] = 'Увеличить',
        ['增加字体大小'] = 'Увеличить размер субтитров',
        ['复制 MediaInfo 信息'] = 'Копировать MediaInfo',
        ['复制文件路径'] = 'Копировать путь к файлу',
        ['复制视频元数据'] = 'Копировать метаданные видео',
        ['字幕'] = 'Субтитры',
        ['对比度 +1'] = 'Контраст +1',
        ['对比度 -1'] = 'Контраст -1',
        ['导出播放列表'] = 'Экспортировать плейлист',
        ['导航'] = 'Навигация',
        ['工具'] = 'Инструменты',
        ['左移'] = 'Переместить влево',
        ['帧位'] = 'Панорама',
        ['常驻显示统计信息'] = 'Постоянная статистика',
        ['延迟 +0.1'] = 'Задержка +0.1',
        ['延迟 -0.1'] = 'Задержка -0.1',
        ['强制开启（仅 SDR 片源）'] = 'Принудительно (только SDR-источник)',
        ['截屏'] = 'Снимок экрана',
        ['打乱播放列表'] = 'Перемешать плейлист',
        ['打开select分菜单-属性列表'] = 'Меню select: свойства',
        ['打开select总菜单'] = 'Открыть меню select',
        ['按键绑定列表'] = 'Список горячих клавиш',
        ['播放'] = 'Воспроизведение',
        ['播放列表'] = 'Плейлист',
        ['播放列表为空'] = 'Плейлист пуст',
        ['放大 +1%'] = 'Увеличить +1%',
        ['时间码解析模式'] = 'Режим разбора таймкода',
        ['显示 MediaInfo 信息'] = 'Показать MediaInfo',
        ['显示 OSD 时间轴'] = 'Показать таймлайн OSD',
        ['显示控制台'] = 'Показать консоль',
        ['显示统计信息'] = 'Показать статистику',
        ['显示进度'] = 'Показать прогресс',
        ['暂停'] = 'Пауза',
        ['条目'] = 'Пункт',
        ['查看'] = 'Вид',
        ['次字幕'] = 'Вторые субтитры',
        ['次字幕选项'] = 'Параметры вторых субтитров',
        ['比例'] = 'Пропорции',
        ['没有可用音轨'] = 'Нет аудиодорожек',
        ['清空所有脚本'] = 'Очистить все скрипты',
        ['清除已记录的属性值'] = 'Очистить сохранённые свойства',
        ['滤镜与增强'] = 'Фильтры и улучшение',
        ['版本'] = 'Издания',
        ['着色器'] = 'Шейдеры',
        ['窗口'] = 'Окно',
        ['窗口-无OSD'] = 'Окно (без OSD)',
        ['章节'] = 'Главы',
        ['缩小 -1%'] = 'Уменьшить -1%',
        ['缩放'] = 'Масштаб',
        ['翻倍'] = 'Вдвое больше',
        ['自动'] = 'Авто',
        ['自动 ICC 配置'] = 'Автоматический ICC-профиль',
        ['自动选择设备'] = 'Автоматическое устройство',
        ['自动（SDR 片源 + 屏幕 HDR）'] = 'Авто (SDR-источник + HDR-экран)',
        ['色调 +1'] = 'Оттенок +1',
        ['色调 -1'] = 'Оттенок -1',
        ['视频'] = 'Видео',
        ['设置/清除 A-B 循环点'] = 'Установить/убрать петлю A-B',
        ['调色'] = 'Цвет',
        ['轨道'] = 'Дорожки',
        ['输出设备'] = 'Устройство вывода',
        ['逆时针旋转'] = 'Повернуть против часовой',
        ['速度'] = 'Скорость',
        ['配置文件'] = 'Профили',
        ['重置'] = 'Сброс',
        ['重置 音频与字幕同步'] = 'Сбросить синхронизацию A/V',
        ['降低'] = 'Уменьшить',
        ['静音'] = 'Отключить звук',
        ['音轨'] = 'Аудиодорожка',
        ['音量'] = 'Громкость',
        ['音频'] = 'Аудио',
        ['顺时针旋转'] = 'Повернуть по часовой',
        ['饱和度 +1'] = 'Насыщенность +1',
        ['饱和度 -1'] = 'Насыщенность -1',
        ['（外部）'] = '(внешняя)',
        ['（强制）'] = '(принудительно)',
        ['（默认）'] = '(по умолчанию)',
    },
}
-- END MENU_I18N

-- BEGIN PREFIX_I18N
local dyn_prefix_i18n = {
    ['en-US'] = {
        ['章节'] = 'Chapter',
        ['版本'] = 'Edition',
    },
    ['ja-JP'] = {
        ['章节'] = 'チャプター',
        ['版本'] = 'エディション',
    },
    ['ko-KR'] = {
        ['章节'] = '챕터',
        ['版本'] = '에디션',
    },
    ['de-DE'] = {
        ['章节'] = 'Kapitel',
        ['版本'] = 'Edition',
    },
    ['fr-FR'] = {
        ['章节'] = 'Chapitre',
        ['版本'] = 'Édition',
    },
    ['es-ES'] = {
        ['章节'] = 'Capítulo',
        ['版本'] = 'Edición',
    },
    ['ru-RU'] = {
        ['章节'] = 'Глава',
        ['版本'] = 'Издание',
    },
}
-- END PREFIX_I18N

local function localize_title(title)
    if not title or title == '' then return title end
    local t = menu_i18n[menu_lang]
    if t and t[title] then return t[title] end
    return title
end

local function localize_prefix(prefix)
    local t = dyn_prefix_i18n[menu_lang]
    if t and t[prefix] then return t[prefix] end
    return prefix
end

local use_mpv_impl = o.use_mpv_impl and (mp.get_property_native('menu-data') ~= nil)
local menu_prop = use_mpv_impl and 'menu-data' or 'user-data/menu/items' -- menu data property
local menu_items = {}                    -- raw menu data
local menu_items_dirty = false           -- menu data dirty flag
local dyn_menus = {}                     -- dynamic menu list
local keyword_to_menu = {}               -- keyword -> menu
local has_uosc = false                   -- uosc installed flag

-- lua expression compiler (copied from mpv auto_profiles.lua)
------------------------------------------------------------------------
local watched_properties = {}  -- indexed by property name (used as a set)
local cached_properties = {}   -- property name -> last known raw value
local properties_to_menus = {} -- property name -> set of menus using it
local have_dirty_menus = false -- at least one menu is marked dirty

-- Used during evaluation of the menu update
local current_menu = nil

-- Cached set of all top-level mpv properities. Only used for extra validation.
local property_set = {}
for _, property in pairs(mp.get_property_native("property-list")) do
    property_set[property] = true
end

local function on_property_change(name, val)
    cached_properties[name] = val
    -- Mark all menus reading this property as dirty, so they get re-evaluated
    -- the next time the script goes back to sleep.
    local dependent_menus = properties_to_menus[name]
    if dependent_menus then
        for menu, _ in pairs(dependent_menus) do
            menu.dirty = true
            have_dirty_menus = true
        end
    end
end

function get(name, default)
    -- Normally, we use the cached value only
    if not watched_properties[name] then
        watched_properties[name] = true
        local res, err = mp.get_property_native(name)
        -- Property has to not exist and the toplevel of property in the name must also
        -- not have an existing match in the property set for this to be considered an error.
        -- This allows things like user-data/test to still work.
        if err == "property not found" and property_set[name:match("^([^/]+)")] == nil then
            msg.error("Property '" .. name .. "' was not found.")
            return default
        end
        cached_properties[name] = res
        mp.observe_property(name, "native", on_property_change)
    end
    -- The first time the property is read we need add it to the
    -- properties_to_menus table, which will be used to mark the menu
    -- dirty if a property referenced by it changes.
    if current_menu then
        local map = properties_to_menus[name]
        if not map then
            map = {}
            properties_to_menus[name] = map
        end
        map[current_menu] = true
    end
    local val = cached_properties[name]
    if val == nil then
        val = default
    end
    return val
end

local function magic_get(name)
    -- Lua identifiers can't contain "-", so in order to match with mpv
    -- property conventions, replace "_" to "-"
    name = string.gsub(name, "_", "-")
    return get(name, nil)
end

local evil_magic = {}
setmetatable(evil_magic, {
    __index = function(table, key)
        -- interpret everything as property, unless it already exists as
        -- a non-nil global value
        local v = _G[key]
        if type(v) ~= "nil" then
            return v
        end
        return magic_get(key)
    end,
})

p = {}
setmetatable(p, {
    __index = function(table, key)
        return magic_get(key)
    end,
})

local function compile_expr(name, s)
    local code, chunkname = "return " .. s, "expr " .. name
    local chunk, err
    if setfenv then -- lua 5.1
        chunk, err = loadstring(code, chunkname)
        if chunk then
            setfenv(chunk, evil_magic)
        end
    else -- lua 5.2
        chunk, err = load(code, chunkname, "t", evil_magic)
    end
    if not chunk then
        msg.error("expr '" .. name .. "' : " .. err)
        chunk = function() return false end
    end
    return chunk
end
------------------------------------------------------------------------

-- append menu item to menu
local function append_menu(menu, item)
    if (item.title and o.escape_title) then
        item.title = item.title:gsub('&', '&&')
    end
    menu[#menu + 1] = item
end

-- escape codec name to make it more readable
local function escape_codec(str)
    if not str or str == '' then return '' end
    if str:find("mpeg2") then return "mpeg2"
    elseif str:find("dvvideo") then return "dv"
    elseif str:find("pcm") then return "pcm"
    elseif str:find("pgs") then return "pgs"
    elseif str:find("subrip") then return "srt"
    elseif str:find("vtt") then return "vtt"
    elseif str:find("dvd_sub") then return "vob"
    elseif str:find("dvb_sub") then return "dvb"
    elseif str:find("dvb_tele") then return "teletext"
    elseif str:find("arib") then return "arib"
    else return str end
end

-- from http://lua-users.org/wiki/LuaUnicode
local UTF8_PATTERN = '[%z\1-\127\194-\244][\128-\191]*'

-- return a substring based on utf8 characters
-- like string.sub, but negative index is not supported
local function utf8_sub(s, i, j)
    local t = {}
    local idx = 1
    for match in s:gmatch(UTF8_PATTERN) do
        if j and idx > j then break end
        if idx >= i then t[#t + 1] = match end
        idx = idx + 1
    end
    return table.concat(t)
end

-- return the length of a utf8 string
local function utf8_len(s)
    local _, count = s:gsub(UTF8_PATTERN, "")
    return count
end

-- abbreviate title if it's too long
local function abbr_title(str)
    if not str or str == '' then return '' end
    if o.max_title_length > 0 and utf8_len(str) > o.max_title_length then
        return utf8_sub(str, 1, o.max_title_length) .. '...'
    end
    return str
end

-- build track title from track metadata
--
-- example:
--        V: Video 1 [h264, 1920x1080, 23.976 fps] (*)        JPN
--        |     |               |                   |          |
--       type  title          hints               default     lang
local function build_track_title(track, prefix, filename)
    local type = track.type
    local title = track.title or ''
    local codec = escape_codec(track.codec)

    -- remove filename from title if it's external track
    if track.external and title ~= '' then
        if filename ~= '' then title = title:gsub(filename .. '%.?', '') end
        if title:lower() == codec:lower() then title = '' end
    end
    -- set a default title if it's empty
    if title == '' then
        local names = { video = localize_title('视频'), audio = localize_title('音轨'), sub = localize_title('字幕') }
        local name = names[type] or type:sub(1, 1):upper() .. type:sub(2, #type)
        title = string.format('%s %d', name, track.id)
    else
        title = abbr_title(title)
    end

    -- build hints from track metadata
    local hints = {}
    local function h(value) hints[#hints + 1] = value end
    if codec ~= '' then h(codec) end
    if track['demux-h'] then
        h(track['demux-w'] and (track['demux-w'] .. 'x' .. track['demux-h'] or track['demux-h'] .. 'p'))
    end
    if track['demux-fps'] then h(string.format('%.5g fps', track['demux-fps'])) end
    if track['audio-channels'] then h(track['audio-channels'] .. localize_title(' 声道')) end
    if track['demux-samplerate'] then h(string.format('%.5g kHz', track['demux-samplerate'] / 1000)) end
    if track['demux-bitrate'] then h(string.format('%.5g kbps', track['demux-bitrate'] / 1000)) end
    if #hints > 0 then title = string.format('%s [%s]', title, table.concat(hints, ', ')) end

    -- put some important info at the end
    if track.forced then title = title .. localize_title('（强制）') end
    if track.external then title = title .. localize_title('（外部）') end
    if track.default then title = title .. localize_title('（默认）') end

    -- prepend a 1-letter type prefix, used when displaying multiple track types
    if prefix then title = string.format('%s: %s', type:sub(1, 1):upper(), title) end
    -- 控制原生菜单宽度：完整标题（含提示信息）也一并截断
    return abbr_title(title)
end

-- build track menu items from track list for given type
local function build_track_items(list, type, prop, prefix)
    local items = {}

    -- filename without extension, escaped for pattern matching
    local filename = get('filename/no-ext', ''):gsub("[%(%)%.%%%+%-%*%?%[%]%^%$]", "%%%0")
    local pos = tonumber(get(prop)) or -1

    for _, track in ipairs(list) do
        if track.type == type then
            local state = {}
            if track.selected and track.id == pos then
                state[#state + 1] = 'checked'
                if type == 'sub' then
                    if (prop == 'sid' and not get('sub-visibility')) or 
                        (prop == 'secondary-sid' and not get('secondary-sub-visibility'))
                    then
                        state[#state + 1] = 'disabled'
                    end
                end
            end

            items[#items + 1] = {
                title = build_track_title(track, prefix, filename),
                shortcut = (track.lang and track.lang ~= '') and track.lang or nil,
                cmd = string.format('set %s %d', prop, track.id),
                state = state,
            }
        end
    end

    -- add an extra item to disable or re-enable the track
    if #items > 0 then
        local title = pos > 0 and localize_title('关闭') or localize_title('自动')
        local value = pos > 0 and 'no' or 'auto'
        if prefix then title = string.format('%s: %s', type:sub(1, 1):upper(), title) end

        items[#items + 1] = {
            title = title,
            cmd = string.format('set %s %s', prop, value),
        }
    end

    return items
end

-- update menu item to a submenu
local function to_submenu(item)
    item.type = 'submenu'
    item.submenu = {}
    item.cmd = nil

    menu_items_dirty = true

    return item.submenu
end

-- handle #@tracks menu update
local function update_tracks_menu(menu)
    local submenu = to_submenu(menu.item)
    local track_list = get('track-list', {})
    if #track_list == 0 then return end

    local items_v = build_track_items(track_list, 'video', 'vid', true)
    local items_a = build_track_items(track_list, 'audio', 'aid', true)
    local items_s = build_track_items(track_list, 'sub', 'sid', true)

    -- append video/audio/sub tracks into one submenu, separated by a separator
    for _, item in ipairs(items_v) do append_menu(submenu, item) end
    if #submenu > 0 and #items_a > 0 then append_menu(submenu, { type = 'separator' }) end
    for _, item in ipairs(items_a) do append_menu(submenu, item) end
    if #submenu > 0 and #items_s > 0 then append_menu(submenu, { type = 'separator' }) end
    for _, item in ipairs(items_s) do append_menu(submenu, item) end
end

-- handle #@tracks/<type> menu update for given type
local function update_track_menu(menu, type, prop)
    local submenu = to_submenu(menu.item)
    local track_list = get('track-list', {})
    if #track_list == 0 then return end

    local items = build_track_items(track_list, type, prop, false)
    for _, item in ipairs(items) do append_menu(submenu, item) end
end

-- handle #@chapters menu update
local function update_chapters_menu(menu)
    local submenu = to_submenu(menu.item)
    local chapter_list = get('chapter-list', {})
    if #chapter_list == 0 then return end

    local pos = get('chapter', -1)
    for id, chapter in ipairs(chapter_list) do
        local title = abbr_title(chapter.title)
        if title == '' then title = localize_prefix('章节') .. ' ' .. id end

        append_menu(submenu, {
            title = title,
            shortcut = string.format('[%02d:%02d:%02d]', chapter.time / 3600, chapter.time / 60 % 60, chapter.time % 60),
            cmd = string.format('seek %f absolute', chapter.time),
            state = id == pos + 1 and { 'checked' } or {},
        })
    end
end

-- handle #@edition menu update
local function update_editions_menu(menu)
    local submenu = to_submenu(menu.item)
    local edition_list = get('edition-list', {})
    if #edition_list == 0 then return end

    local current = get('current-edition', -1)
    for id, edition in ipairs(edition_list) do
        local title = abbr_title(edition.title)
        if title == '' then title = localize_prefix('版本') .. ' ' .. id end
        if edition.default then title = title .. localize_title('[默认]') end
        append_menu(submenu, {
            title = title,
            cmd = string.format('set edition %d', id - 1),
            state = id == current + 1 and { 'checked' } or {},
        })
    end
end

-- handle #@audio-devices menu update
local function update_audio_devices_menu(menu)
    local submenu = to_submenu(menu.item)
    local device_list = get('audio-device-list', {})
    if #device_list == 0 then return end

    local current = get('audio-device', '')
    for _, device in ipairs(device_list) do
        local dev_title = device.name == 'auto' and localize_title('自动选择设备')
            or device.description or device.name
        append_menu(submenu, {
            title = dev_title,
            cmd = string.format('set audio-device %s', device.name),
            state = device.name == current and { 'checked' } or {},
        })
    end
end

-- build playlist item title
local function build_playlist_title(item, id)
    local title = item.title or ''
    local ext = ''
    if item.filename and item.filename ~= '' then
        local _, filename = utils.split_path(item.filename)
        local n, e = filename:match('^(.+)%.([%w-_]+)$')
        if title == '' then title = n and n or filename end
        if e then ext = e end
    end
    title = title ~= '' and abbr_title(title) or localize_title('条目') .. ' ' .. id
    return title, ext
end

-- handle #@playlist menu update
local function update_playlist_menu(menu)
    local submenu = to_submenu(menu.item)
    local playlist = get('playlist', {})
    if #playlist == 0 then return end

    local from, to = 1, #playlist
    if o.max_playlist_items > 0 then
        local pos = get('playlist-playing-pos', -1)
        if pos == -1 then pos = get('playlist-pos', -1) end
        local mid = math.floor(o.max_playlist_items / 2)
        from, to = pos + 1 - mid, pos + (o.max_playlist_items - mid)
        if from < 1 then from, to = 1, o.max_playlist_items end
        if to > #playlist then from, to = #playlist - o.max_playlist_items + 1, #playlist end
    end

    if from > 1 then
        append_menu(submenu, {
            title = '...',
            shortcut = string.format('[%d]', from - 1),
            cmd = has_uosc and 'script-message-to uosc playlist' or 'ignore',
        })
    end

    for id = from, to do
        local item = playlist[id]
        if item then
            local title, ext = build_playlist_title(item, id - 1)
            append_menu(submenu, {
                title = build_playlist_title(item, id - 1),
                shortcut = (ext and ext ~= '') and ext:upper() or nil,
                cmd = string.format('playlist-play-index %d', id - 1),
                state = (item.playing or item.current) and { 'checked' } or {},
            })
        end
    end

    if to < #playlist then
        append_menu(submenu, {
            title = '...',
            shortcut = string.format('[%d]', #playlist - to),
            cmd = has_uosc and 'script-message-to uosc playlist' or 'ignore',
        })
    end
end

-- handle #@profiles menu update
local function update_profiles_menu(menu)
    local submenu = to_submenu(menu.item)
    local profile_list = get('profile-list', {})
    if #profile_list == 0 then return end

    for _, profile in ipairs(profile_list) do
        if not (profile.name == 'default' or profile.name:find('gui') or
                profile.name == 'encoding' or profile.name == 'libmpv') then
            append_menu(submenu, {
                title = profile.name,
                cmd = string.format('show-text %s; apply-profile %s', profile.name, profile.name),
            })
        end
    end
end

-- handle menu state update
local function update_menu_state(menu)
    if not menu.state then return end
    local status, res = pcall(menu.state)
    if not status then
        msg.verbose("state expr error on evaluating: " .. res)
        return
    end

    local state = {}
    if type(res) == 'string' then
        for s in res:gmatch('[^,%s]+') do state[#state + 1] = s end
    end
    menu.item.state = state
    menu_items_dirty = true
end

-- dynamic menu updaters
local dyn_updaters = {
    ['tracks'] = update_tracks_menu,
    ['tracks/video'] = function(menu) update_track_menu(menu, 'video', 'vid') end,
    ['tracks/audio'] = function(menu) update_track_menu(menu, 'audio', 'aid') end,
    ['tracks/sub'] = function(menu) update_track_menu(menu, 'sub', 'sid') end,
    ['tracks/sub-secondary'] = function(menu) update_track_menu(menu, 'sub', 'secondary-sid') end,
    ['chapters'] = update_chapters_menu,
    ['editions'] = update_editions_menu,
    ['audio-devices'] = update_audio_devices_menu,
    ['playlist'] = update_playlist_menu,
    ['profiles'] = update_profiles_menu,
}

-- handle dynamic menu update
local function update_menu(menu)
    if menu.updater then
        msg.debug('update menu: ' .. menu.item.title)
        current_menu = menu
        menu.updater(menu)
        current_menu = nil
    end
end

-- load dynamic menu item
local function dyn_menu_load(item, keyword)
    local menu = {
        item = item,
        updater = nil,
        state = nil,
        dirty = false,
    }
    dyn_menus[#dyn_menus + 1] = menu
    keyword_to_menu[keyword] = menu

    local expr = keyword:match('^state=(.-)%s*$')
    if expr then
        menu.updater = update_menu_state
        menu.state = compile_expr(string.format('[%s]:%s', item.title, keyword), expr)
    else
        keyword = keyword:match('^([%S]+).*$')
        menu.updater = dyn_updaters[keyword]
    end

    -- update menu immediately
    if menu.updater then update_menu(menu) end
end

-- find #@keyword for dynamic menu and handle updates
--
-- cplugin will keep the trailing comments in the cmd field, so we can
-- parse the keyword from it.
--
-- example: ignore        #menu: Chapters #@chapters    # extra comment
local function dyn_menu_check(items)
    if not items then return end
    for _, item in ipairs(items) do
        if item.type == 'submenu' then
            dyn_menu_check(item.submenu)
        else
            if item.type ~= 'separator' and item.cmd then
                local keyword = item.cmd:match('%s*#@(.-)%s*$') or ''
                if keyword ~= '' then
                    msg.debug('load menu: ' .. item.title, ', keyword: ' .. keyword)
                    dyn_menu_load(item, keyword)
                end
            end
        end
    end
end

-- load dynamic menus
local function load_dyn_menus()
    dyn_menu_check(menu_items)

    -- broadcast menu ready message
    mp.commandv('script-message', 'menu-ready', mp.get_script_name())
end

-- read input.conf content
local function get_input_conf()
    local prop = mp.get_property_native('input-conf')
    if prop:sub(1, 9) == 'memory://' then return prop:sub(10) end

    prop = prop == '' and '~~/input.conf' or prop
    local conf_path = mp.command_native({ 'expand-path', prop })

    local f, err = io.open(conf_path, 'rb')
    if not f then
        msg.error('failed to open file: ' .. conf_path)
        return nil
    end

    local conf = f:read('*all')
    f:close()
    return conf
end

-- parse input.conf, return menu items
local function parse_input_conf(conf)
    local function parse_line(line)
        local c = line:match('^%s*#')
        if c and (not o.uosc_syntax) then return end
        local key, cmd = line:match('%s*([%S]+)%s+(.-)%s*$')
        if key and key:match('^#%S+') then return end
        return ((o.uosc_syntax and c) and '' or key), cmd
    end

    local function extract_title(cmd)
        if not cmd or cmd == '' then return '' end
        local title = cmd:match('#menu:%s*(.*)%s*')
        if not title and o.uosc_syntax then title = cmd:match('#!%s*(.*)%s*') end
        if title then title = title:match('(.-)%s*#.*$') or title end
        return title or ''
    end

    local function split_title(title)
        local list = {}
        if not title or title == '' then return list end

        local pattern = '(.-)%s*>%s*'
        local last_ends = 1
        local starts, ends, match = title:find(pattern)
        while starts do
            list[#list + 1] = match
            last_ends = ends + 1
            starts, ends, match = title:find(pattern, last_ends)
        end
        if last_ends < (#title + 1) then list[#list + 1] = title:sub(last_ends) end

        return list
    end

    local items = {}
    local by_id = {}

    for line in conf:gmatch('[^\r\n]+') do
        local key, cmd = parse_line(line)
        local list = split_title(extract_title(cmd))

        local submenu_id = ''
        local target_menu = items

        for id, name in ipairs(list) do
            if id < #list then
                submenu_id = submenu_id .. name
                if not by_id[submenu_id] then
                    local submenu = {}
                    by_id[submenu_id] = submenu
                    append_menu(target_menu, { type = 'submenu', title = localize_title(name), submenu = submenu })
                end
                target_menu = by_id[submenu_id]
            else
                if name == '-' or (o.uosc_syntax and name:sub(1, 3) == '---') then
                    append_menu(target_menu, { type = 'separator' })
                else
                    local shortcut = (key ~= '' and key ~= '_') and key or nil
                    append_menu(target_menu, { title = localize_title(name), shortcut = shortcut, cmd = cmd })
                end
            end
        end
    end

    return items
end

-- script message: get <keyword> <src>
mp.register_script_message('get', function(keyword, src)
    if not src or src == '' then
        msg.debug('get: ignored message with empty src')
        return
    end

    local menu = keyword_to_menu[keyword]
    local reply = { keyword = keyword }
    if menu then reply.item = menu.item else reply.error = 'keyword not found' end
    mp.commandv('script-message-to', src, 'menu-get-reply', utils.format_json(reply))
end)

-- script message: update <keyword> <json>
mp.register_script_message('update', function(keyword, json)
    local menu = keyword_to_menu[keyword]
    if not menu then
        msg.debug('update: ignored message with invalid keyword:', keyword)
        return
    end

    local data, err = utils.parse_json(json)
    if err then msg.error('update: failed to parse json:', err) end
    if not data or next(data) == nil then
        msg.debug('update: ignored message with invalid json:', json)
        return
    end

    local item = menu.item
    if not data.title or data.title == '' then data.title = item.title end
    if not data.type or data.type == '' then data.type = item.type end

    for k, _ in pairs(item) do item[k] = nil end
    for k, v in pairs(data) do item[k] = v end

    menu_items_dirty = true
end)

-- detect uosc installation
mp.register_script_message('uosc-version', function() has_uosc = true end)

-- 播放列表按钮专用菜单（本地补丁）：通过 menu.dll 的独立临时通道
-- （user-data/menu/temp-items + show-temp）弹出 Win32 原生菜单，
-- 不修改共享的右键菜单数据，弹完即弃、无竞态。
local restore_timer = nil

local function restore_full_menu()
    restore_timer = nil
    menu_items_dirty = true
end

-- menu.dll 渲染路径的脚本名（menu-init 消息更新）
local menu_native = 'menu'

mp.register_script_message('playlist-menu', function()
    local playlist = mp.get_property_native('playlist') or {}
    if #playlist == 0 then
        mp.commandv('show-text', localize_title('播放列表为空'), 1500)
        return
    end

    local items = {}
    for id, item in ipairs(playlist) do
        local title, ext = build_playlist_title(item, id - 1)
        append_menu(items, {
            title = title,
            shortcut = (ext and ext ~= '') and ext:upper() or nil,
            cmd = string.format('playlist-play-index %d', id - 1),
            state = (item.playing or item.current) and {'checked'} or {},
        })
    end

    if use_mpv_impl then
        if restore_timer then restore_timer:kill() end
        mp.set_property_native(menu_prop, items)
        mp.commandv('context-menu')
        restore_timer = mp.add_timeout(0.5, restore_full_menu)
    else
        -- 独立临时通道：不动共享 menu-data，无竞态，弹完即弃
        mp.set_property_native('user-data/menu/temp-items', items)
        mp.commandv('script-message-to', menu_native, 'show-temp')
    end
end)

-- 音频按钮专用菜单（本地补丁）：与 playlist-menu 相同，弹出原生 Win32 音轨菜单
mp.register_script_message('audio-menu', function()
    local track_list = mp.get_property_native('track-list') or {}
    local items = {}
    local audio_items = build_track_items(track_list, 'audio', 'aid', false)
    if #audio_items == 0 then
        mp.commandv('show-text', localize_title('没有可用音轨'), 1500)
        return
    end
    for _, item in ipairs(audio_items) do append_menu(items, item) end

    if use_mpv_impl then
        if restore_timer then restore_timer:kill() end
        mp.set_property_native(menu_prop, items)
        mp.commandv('context-menu')
        restore_timer = mp.add_timeout(0.5, restore_full_menu)
    else
        mp.set_property_native('user-data/menu/temp-items', items)
        mp.commandv('script-message-to', menu_native, 'show-temp')
    end
end)

-- update menu on idle, this reduces the update frequency
mp.register_idle(function()
    if have_dirty_menus then
        for _, menu in ipairs(dyn_menus) do
            if menu.dirty then
                update_menu(menu)
                menu.dirty = false
            end
        end
        have_dirty_menus = false
    end

    if menu_items_dirty then
        msg.debug('commit menu items: ' .. menu_prop)
        mp.set_property_native(menu_prop, menu_items)
        menu_items_dirty = false
    end
end)

-- menu implementation related initialization
local function show_button_menu()
    if use_mpv_impl then
        mp.commandv('context-menu')
    else
        mp.commandv('script-message-to', menu_native, 'show')
    end
end

local function reset_and_show_menu()
    if restore_timer then
        restore_timer:kill()
        restore_timer = nil
        menu_items_dirty = true
    end
    show_button_menu()
end

if use_mpv_impl then
    -- IMPORTANT: make menu work on vo change
    mp.observe_property('current-vo', 'native', function(name, val)
        if val then menu_items_dirty = true end
    end)

    mp.add_key_binding('MBTN_RIGHT', nil, reset_and_show_menu)
else
    mp.register_script_message('menu-init', function(name)
        menu_native = name
    end)

    mp.add_key_binding('MBTN_RIGHT', 'show', reset_and_show_menu)
end

-- load menu data from input.conf
--
-- NOTE: to simplify the code, we don't watch for the menu data change event, this
--       make it conflict with other scripts that also update the menu data property.
local input_conf_text = get_input_conf()
local conf = input_conf_text
if conf then
    menu_items = parse_input_conf(conf)
    menu_items_dirty = true
    load_dyn_menus()
end

-- 界面语言变化时（应用设置后重启前也会写入）重解析菜单
mp.observe_property('user-data/mpvw/language', 'string', function()
    menu_lang = mp.get_property_native('user-data/mpvw/language') or 'en-US'
    if input_conf_text then
        menu_items = parse_input_conf(input_conf_text)
        menu_items_dirty = true
    end
end)
