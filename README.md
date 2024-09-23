# LeinneBeat
Welcome to the world of beats ruled by Leinne

## 실행 전 주의사항
하단의 경로가 존재하는지 확인해야합니다.
존재하지 않는다면 오류가 발생할 수 있습니다.
* `LeinneBeat\Songs\`
* `LeinneBeat\Theme\`

## 마커 설정 방법
마커는 `LeinneBeat\Theme\marker\` 폴더에서 이미지를 가져오도록 되어있습니다.  
마커의 폴더 구조는 다음과 같습니다.
* normal
  * png files...
* perfect
  * png files...
* great
  * png files...
* good
  * png files...
* poor
  * png files...

마커의 sample rate는 30fps 이며 이미지 해상도는 제약은 없으나 `400x400`이상을 추천합니다.  
이미지의 이름 규칙은 따로 없으며 오름차순으로 읽어옵니다.

## 곡 추가 방법
경로: `LeinneBeat\Songs\` 에 아래의 조건을 충족하는 `곡 폴더`를 추가해주세요. (**굵은 글씨는 필수 파일입니다**)  

* LeinneBeat\Songs
  * **Example** `곡 폴더`
    * basic.txt
    * advanced.txt
    * extreme.txt
    * **info.json**
    * jacket.\[png|jpg|jpeg|...]
    * **song.\[ogg|wav|mp3]**

### txt 파일
채보 파일은 4x4 형태의 memo 형식을 기본적으로 지원합니다.  
채보 형식은 다음과 같습니다.  
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
`LEVEL`은 아래와 같이 작성 가능합니다.
* `lev=10.9`
* `#lev=10.9`
* `Level: 10.9`

`BPM`은 아래와 같이 작성 가능합니다.
* `t=100`
* `#t=100`
* `BPM: 100`
* 
#### 채보 추가 지원
[memon](https://memon-spec.readthedocs.io/en/latest/) 지원을 준비중에있습니다.

### info.json
곡의 기본정보가 작성돼있는 파일입니다.
```json
{
  "title": "title",
  "artist": "artist",
  "offset": 0.0,
  "preview": 35.0,
  "duration": 10.0
}
```
