# LeinneBeat
Welcome to the world of beats ruled by Leinne

## 띵즈 투 체크 비포 러닝
`LeinneBeat\Songs\` 와 `LeinneBeat\Theme\marker` 폴더 체크 이즈 이그지스트  
더 스트럭트 오브 더 `LeinneBeat\Theme\marker` 아래와 라이크하게 세팅 되어야함
```
marker
ㄴ normal
  ㄴ png...
ㄴ perfect
  ㄴ png...
ㄴ great
  ㄴ png...
ㄴ good
  ㄴ png...
ㄴ poor
  ㄴ png...
```
마커는 한장 = 1프레임, 리프레시 레이트는 30fps  
Image Names 규칙 딱히 없음. `어센딩`하게 읽어오니 걱정 노노  
해당 폴더 낫 이그지스트 할시 \<Error/>  

## 하우 투 레지스터 어 송
경로: `LeinneBeat\Songs\` 에 `곡 폴더` 삽입  
곡 폴더에는 다음과 같은 항목이 폴더에 있어야함(**Files in bold are required**)
* basic.txt
* advanced.txt
* extreme.txt
* info.json
* jacket.\[png|jpg|jpeg|...]
* **song.\[ogg|wav|mp3]**

### txt 파일
지원 형식은 memo 형태의 타입  

`LEVEL`은 다음중 하나
* `lev=10.9`
* `#lev=10.9`
* `Level: 10.9`

`BPM`은 다음중 하나
* `t=100`
* `#t=100`
* `BPM: 200`
```
LEVEL
BPM

1
口口口口 |－－－－|
口口口口 |－－－－|
口口口口 |－－－－|
口口口口 |－－－－|

2
口口口口 |－－－－|
...
```

### info.json
해당파일은 없어도 자동으로 생성됨  
형태는 아래와 같음
```json
{
  "title": "title",
  "artist": "artist",
  "offset": 0.0,
  "preview": 35.0,
  "duration": 10.0
}
```
