# Clipboard

Windows 클립보드 기록 앱 (WPF, .NET 8).

- 시스템 트레이 상주 (더블클릭 또는 **Win+V** 로 열기)
- 사용(붙여넣기)하거나 다시 복사한 항목은 항상 목록 맨 위로 이동
- 이미지는 작은 썸네일로 표시하고, Windows 내장 OCR로 글자를 인식해 검색 가능
- Windows 시작 시 자동 실행 (첫 실행 시 켜짐, 트레이 메뉴에서 해제 가능)
- 목록: ↑↓ 이동 · Enter/클릭 붙여넣기 · Ctrl+1~0 상위 10개 바로 붙여넣기 · Del 삭제 · Esc 닫기 · 입력하여 검색

## 빌드

```
dotnet publish src/ClipboardApp.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Windows 10 1903+ / .NET 8 Desktop Runtime 필요. 기록은 `%AppData%\ClipboardApp` 에 저장됩니다.
OCR 언어는 Windows에 설치된 언어 팩을 사용합니다 (한국어 인식은 한국어 언어 팩 필요).
