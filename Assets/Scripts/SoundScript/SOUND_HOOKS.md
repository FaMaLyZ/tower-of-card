# จุดใส่เสียง (Sound hooks)

ทุกไฟล์อยู่ในโฟลเดอร์ [Assets/Scripts/Juice/](../Juice/) คลิกลิงก์แล้วเด้งไปบรรทัดนั้นได้เลย

### ✅ มีฟังก์ชัน `Play...`: วางเสียงตรงนี้ แล้วกด F8 ทดสอบได้

| เหตุการณ์ | ฟังก์ชันที่วางเสียง | ปุ่ม F8 |
|---|---|---|
| slam ตอน Play hand / ตอนคะแนนเด้ง | [HandPlaqueJuice.cs:235](../Juice/HandPlaqueJuice.cs#L235) `SlamInto` | ปุ่มชื่อมือ, "Slam at this drama" |
| "+11" เด้ง | [FloatingNumbers.cs:81](../Juice/FloatingNumbers.cs#L81) `Pop` | "Score pops and flies to the Pot" |
| คะแนนบินเข้า Pot | [FloatingNumbers.cs:89](../Juice/FloatingNumbers.cs#L89) `Fly` | อันเดียวกัน |
| Pot เสียแต้ม | [PotSignJuice.cs:110](../Juice/PotSignJuice.cs#L110) `PlayLoss` | "Cards broke" |
| Pot แตก | [PotSignJuice.cs:118](../Juice/PotSignJuice.cs#L118) `PlayShatter` | "Collapse" |
| Glass แตก | [GlassJuice.cs:132](../Juice/GlassJuice.cs#L132) `PlayCrack` | "Crack burst" |
| Glass ซ่อม | [GlassJuice.cs:288](../Juice/GlassJuice.cs#L288) `PlayRepair` | "Heal" |
| WON! / FAILED | [BlindResultJuice.cs:74](../Juice/BlindResultJuice.cs#L74) `Play` | "WON!" / "FAILED" |
| ANTE n | [BlindMenuJuice.cs:75](../Juice/BlindMenuJuice.cs#L75) `PlayAnte` | "ANTE 3" |
| จบรัน (ชนะ / แพ้) | [RunEndJuice.cs:63](../Juice/RunEndJuice.cs#L63) `Play` | "RUN CLEARED" / "GLASS SHATTERED" |
| Intro ไพ่บินเข้ากอง | [RunIntroJuice.cs:144](../Juice/RunIntroJuice.cs#L144) `Play` | "Run intro" |
| ไพ่แต่ละใบลงกองตอน intro | [DeckJuice.cs:154](../Juice/DeckJuice.cs#L154) `PlayIntroLanding` | "Run intro" |
| กองไพ่โดน slam | [DeckJuice.cs:183](../Juice/DeckJuice.cs#L183) `PlaySlam` | "Deck slam" |
| ไพ่ในมือกระโดด | [HandJuice.cs:134](../Juice/HandJuice.cs#L134) `PlaySlamJump` | "Hand cards jump" |
