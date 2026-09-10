# Smart Game — Kumpulan Praktikum

Mata Kuliah **Game Cerdas** — S1 Teknik Informatika, Semester 7
Unity **6000.3.23f1**

Repository ini menampung seluruh praktikum mata kuliah. **Setiap praktikum adalah
Unity project yang berdiri sendiri** dan dibuka terpisah lewat Unity Hub —
bukan satu project besar.

| Folder | Praktikum | Topik |
|---|---|---|
| [`Praktikum-01-NPCDetector`](Praktikum-01-NPCDetector) | Praktikum 1 | NPC Detector |
| [`Praktikum-02-NPCGuard`](Praktikum-02-NPCGuard) | Praktikum 2 | NPC Guard: Sensor + Memory + Decision |

## Kenapa dipisah per folder?

Kedua project punya setting yang tidak bisa disatukan:

| | Praktikum 01 | Praktikum 02 |
|---|---|---|
| Render pipeline | URP | Built-in 3D |
| Input | New Input System | Input Manager lama (sesuai modul) |
| `PlayerController.cs` | ada | ada, implementasi berbeda |

Render pipeline adalah setting per-project, dan dua kelas `PlayerController`
dalam satu project menyebabkan error compile. Karena itu masing-masing tetap
menjadi project terpisah di dalam satu repository.

## Cara membuka

Unity Hub → **Add** → **Add project from disk** → pilih salah satu folder
praktikum (bukan folder root repository ini).

## Riwayat commit

Folder praktikum digabungkan dengan `git subtree`, sehingga riwayat commit asli
dari kedua repository ikut terbawa:

```bash
git log --oneline -- Praktikum-01-NPCDetector
git log --oneline -- Praktikum-02-NPCGuard
```
