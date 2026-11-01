# Petrichor 1.0.6 · Mooic UI port QA

final result: passed — reviewed primary screens and documented adaptations

The existing MAUI package app.petrichor.music uses the original Petrichor backend. Core source is unchanged.

Reference: actual Mooic 3.0.1 emulator captures in artifacts/mooic-reference, plus tagged upstream UI source. Implementation: matching 1080×2400 portrait Android 15 captures in artifacts/maui-mooic-port. Home, Player, Lyrics, Library and Explore were opened and visually compared. Dynamic theme color and music content differ; the lyric reference is dark and the implementation is light.

Fixed: oversized promotional Home header; oversized / disconnected player space; lyrics confined to cover area; duplicated now-playing caption; oversized slider thumb; empty mini-player placeholder; duplicated search title; inaccessible hidden-tab routes after simplifying navigation.

Verification: 119 Core checks; 17 Release UI navigation / cache / system-picker checks; real local FLAC playback; native seek reached 49.8 s / 90 s and wave screenshot was inspected. Actual ~7.06 s motion recording and extracted frames were inspected. Installed APK hash equals the delivered APK hash.

No real-account SMS, password success, membership lossless or playlist writes were tested. No comparable performance benchmark or full TalkBack / landscape audit is claimed. During artwork expansion the native overlay enlarges a mini-player bitmap before the full-size cover takes over; intermediate artwork may look softer.

Evidence and feature mapping: docs/UI-1.0.6.md and artifacts/maui-mooic-port/index.html.
