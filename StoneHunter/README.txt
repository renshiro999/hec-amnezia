# StoneHunter (Windows 11)

Pachet sursă pentru generarea aplicației portabile `StoneHunter.exe`.
Aplicația finală este publicată self-contained: pe PC-ul pe care rulează nu trebuie instalate Python sau .NET.

## Important
- Pachetul de aici NU conține încă executabilul compilat. Mediul în care a fost pregătit nu are compilator Windows/.NET, deci nu pot pretinde că am produs sau testat un `.exe`.
- Detecția folosește potrivire de imagine cu mai multe scări și rotații; este o metodă euristică, nu AI. Rezultatul depinde de rezoluție, iluminare, unghiul camerei și cât de mult diferă piatra de exemplul furnizat.
- Folosește-l doar în jocuri în care automatizarea este permisă. Unele jocuri pot interzice auto-clickerele.

## Cum obții EXE-ul fără Python pe calculatorul tău
1. Creează un repository GitHub nou și încarcă toate fișierele din acest ZIP (inclusiv folderul `.github`).
2. În repository, deschide **Actions** → **Build StoneHunter for Windows** → **Run workflow**.
3. După ce termină, deschide rularea și descarcă artifact-ul **StoneHunter-Windows-x64**.
4. Dezarhivează-l și pornește `StoneHunter.exe`. Nu trebuie să instalezi Python sau .NET.

## Utilizare
- `piatra.png` este imaginea de referință decupată din captura trimisă.
- Deschide jocul, apoi StoneHunter.
- Alege altă imagine dacă modelul din joc arată diferit.
- Reglează sensibilitatea; o valoare mai mare reduce click-urile false.
- Apasă START pentru scanare și STOP pentru oprire.

## Limitări
Versiunea inițială scanează ecranul principal și face click în centrul detecțiilor. Pentru jocuri cu anti-cheat, obiecte foarte mici, schimbări puternice de perspectivă sau pietre care arată diferit, poate necesita ajustări.
