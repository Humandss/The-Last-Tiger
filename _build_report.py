# -*- coding: utf-8 -*-
import os
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, PageBreak,
                                Table, TableStyle, KeepTogether)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from xml.sax.saxutils import escape

# ---------- fonts ----------
pdfmetrics.registerFont(TTFont('Malgun',   r'C:\Windows\Fonts\malgun.ttf'))
pdfmetrics.registerFont(TTFont('MalgunBd', r'C:\Windows\Fonts\malgunbd.ttf'))
pdfmetrics.registerFontFamily('Malgun', normal='Malgun', bold='MalgunBd',
                              italic='Malgun', boldItalic='MalgunBd')

OUT = r'C:\Users\Babo\Documents\GitHub\The-Last-Tiger\게임패턴_리팩토링_보고서.pdf'

# ---------- styles ----------
INK = colors.HexColor('#1a1a1a')
body = ParagraphStyle('body', fontName='Malgun', fontSize=10.5, leading=16.5,
                      alignment=TA_JUSTIFY, spaceAfter=6, textColor=INK)
h1 = ParagraphStyle('h1', fontName='MalgunBd', fontSize=15.5, leading=20,
                    spaceBefore=16, spaceAfter=9, textColor=colors.HexColor('#10243e'))
h2 = ParagraphStyle('h2', fontName='MalgunBd', fontSize=12, leading=16,
                    spaceBefore=11, spaceAfter=5, textColor=colors.HexColor('#1f3a5f'))
code = ParagraphStyle('code', fontName='Malgun', fontSize=8.6, leading=13,
                      backColor=colors.HexColor('#f4f5f7'),
                      borderColor=colors.HexColor('#d9dce1'), borderWidth=0.6,
                      borderPadding=7, leftIndent=2, rightIndent=2,
                      spaceBefore=4, spaceAfter=9, textColor=colors.HexColor('#16213a'))
note = ParagraphStyle('note', fontName='Malgun', fontSize=9.6, leading=15,
                      textColor=colors.HexColor('#555'), leftIndent=8,
                      spaceBefore=2, spaceAfter=8)
cover_t = ParagraphStyle('ct', fontName='MalgunBd', fontSize=25, leading=34, alignment=TA_CENTER, textColor=INK)
cover_s = ParagraphStyle('cs', fontName='Malgun', fontSize=13, leading=20, alignment=TA_CENTER, textColor=colors.HexColor('#444'))
toc_s = ParagraphStyle('toc', fontName='Malgun', fontSize=11.5, leading=22, textColor=INK)
cell = ParagraphStyle('cell', fontName='Malgun', fontSize=9.4, leading=13.5, textColor=INK)
cellh = ParagraphStyle('cellh', fontName='MalgunBd', fontSize=9.4, leading=13.5, textColor=colors.white)


def P(t, st=body):
    return Paragraph(t, st)


def codeblk(txt):
    out = []
    for line in txt.split('\n'):
        s = line.lstrip(' ')
        ind = len(line) - len(s)
        out.append('&nbsp;' * ind + escape(s))
    return Paragraph('<br/>'.join(out), code)


def tbl(rows, widths, header=True):
    data = []
    for i, row in enumerate(rows):
        st = cellh if (header and i == 0) else cell
        data.append([Paragraph(escape(str(c)), st) for c in row])
    t = Table(data, colWidths=widths, hAlign='LEFT')
    ts = [('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#c7ccd3')),
          ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
          ('LEFTPADDING', (0, 0), (-1, -1), 6), ('RIGHTPADDING', (0, 0), (-1, -1), 6),
          ('TOPPADDING', (0, 0), (-1, -1), 5), ('BOTTOMPADDING', (0, 0), (-1, -1), 5),
          ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor('#f7f8fa')])]
    if header:
        ts.append(('BACKGROUND', (0, 0), (-1, 0), colors.HexColor('#2c3e50')))
    t.setStyle(TableStyle(ts))
    return t


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont('Malgun', 9)
    canvas.setFillColor(colors.HexColor('#888'))
    canvas.drawCentredString(A4[0] / 2, 1.2 * cm, str(doc.page))
    canvas.restoreState()


W = 16.6 * cm  # content width
story = []

# ===================== COVER =====================
story += [Spacer(1, 4.2 * cm),
          P('게임 프로그래밍 패턴 적용<br/>리팩토링 보고서', cover_t),
          Spacer(1, 0.7 * cm),
          P('— Unity 탱크 시뮬레이션 「The Last Tiger」를 중심으로 —', cover_s),
          Spacer(1, 3.2 * cm)]
info = [['과목', '게임 프로그래밍 패턴'],
        ['담당 교수', '(교수명)'],
        ['학과 / 학번', '(학과) / (학번)'],
        ['이름', '(이름)'],
        ['제출일', '2026. 06. 11.']]
it = Table([[Paragraph(escape(a), cellh), Paragraph(escape(b), cell)] for a, b in info],
           colWidths=[3.4 * cm, 7.4 * cm], hAlign='CENTER')
it.setStyle(TableStyle([
    ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor('#c7ccd3')),
    ('BACKGROUND', (0, 0), (0, -1), colors.HexColor('#2c3e50')),
    ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
    ('LEFTPADDING', (0, 0), (-1, -1), 8), ('RIGHTPADDING', (0, 0), (-1, -1), 8),
    ('TOPPADDING', (0, 0), (-1, -1), 7), ('BOTTOMPADDING', (0, 0), (-1, -1), 7)]))
story += [it, PageBreak()]

# ===================== TOC =====================
story.append(P('목차', h1))
for t in ['1. 서론', '2. 대상 프로젝트 개요', '3. 현행 아키텍처 분석',
          '4. 패턴 적용 ① Observer — 모듈 손상 전파',
          '5. 패턴 적용 ② Object Pool + Budget — 자원 관리',
          '6. 패턴 적용 ③ Command — STT 음성 명령',
          '7. 적용 결과 및 고찰', '8. 결론 / 참고문헌']:
    story.append(P(t, toc_s))
story.append(PageBreak())

# ===================== 1. 서론 =====================
story.append(P('1. 서론', h1))
story.append(P('1.1 배경 및 목적', h2))
story.append(P('본 보고서는 직접 개발한 프로젝트에 GoF 및 게임 프로그래밍 패턴을 적용·분석하고, 그 효과와 <b>한계</b>를 함께 고찰하는 것을 목적으로 한다. 단순 적용에 그치지 않고, 각 패턴이 실제 코드에서 어떤 문제를 해결하며 어떤 비용을 수반하는지를 코드 레벨에서 검증한다.'))
story.append(P('1.2 보고서 범위', h2))
story.append(P('· <b>프로젝트 A</b> : Unity 탱크 시뮬레이션 「The Last Tiger」 — Observer / Object Pool+Budget / Command 3개 패턴'))
story.append(P('· <b>프로젝트 B</b> : (두 번째 프로젝트명) — (적용 패턴)'))

# ===================== 2. 대상 프로젝트 =====================
story.append(P('2. 대상 프로젝트 개요', h1))
story.append(P('2.1 프로젝트 A — The Last Tiger', h2))
story.append(P('Unity(URP) 기반 1인칭 탱크 시뮬레이션. 물리 기반 탄도·장갑 관통, 모듈 단위 손상, 음성 인식(STT) 승무원 지휘, AI 교전 등 <b>시뮬레이션 깊이</b>를 지향한다. 스크립트는 Shell(탄도)·Armor(장갑)·Module(손상)·AI·STT·Crew·Optimization 등으로 구성된다.'))
story.append(P('2.2 프로젝트 B — (두 번째 프로젝트)', h2))
story.append(P('<i>(작성 예정)</i>', note))

# ===================== 3. 현행 아키텍처 =====================
story.append(P('3. 현행 아키텍처 분석', h1))
story.append(P('리팩토링 대상 선정에 앞서 현행 코드에 이미 적용된 설계를 조사하였다.'))
story.append(tbl([['영역', '적용 설계', '비고'],
                  ['자원 관리', 'Object Pool + Singleton + Budget/LRU', 'PoolManager 외'],
                  ['손상 전파', 'Observer (event 기반)', 'ModuleDamageController'],
                  ['데이터 정의', 'ScriptableObject 데이터주도', 'ShellData, AIProfile'],
                  ['AI', 'enum 상태기계', 'TankAIController'],
                  ['입력', 'Command 맹아 (Queue 디스패치)', 'CrewCommandDispatcher']],
                 [3.0 * cm, 7.6 * cm, 6.0 * cm]))
story.append(Spacer(1, 6))
story.append(P('이 중 <b>① 통신(Observer) · ② 자원(Object Pool) · ③ 입력(Command)</b> 의 세 축을 분석 대상으로 선정하였다. 서로 책임 영역이 겹치지 않아 시스템의 상이한 측면을 균형 있게 다룰 수 있기 때문이다.'))

# ===================== 4. Observer =====================
story.append(P('4. 패턴 적용 ① Observer — 모듈 손상 전파', h1))
story.append(P('4.1 문제 정의 (Before)', h2))
story.append(P('탱크는 엔진·변속기·궤도·포신·장전수 등 다수 모듈로 구성되며, 한 모듈의 손상은 주행·사격·장전·이펙트·사운드·AI 등 <b>다수 시스템에 동시에 영향</b>을 준다. 손상 처리 코드가 이들을 직접 호출하면 강결합이 발생하고, 반응 시스템을 추가할 때마다 손상 코드를 수정해야 한다.'))
story.append(P('4.2 패턴 구조 (역할 매핑)', h2))
story.append(tbl([['Observer 역할', '구현'],
                  ['Subject (발신)', 'ModuleDamageController — event OnDamaged / OnStateChanged / OnHit'],
                  ['Observer (수신)', '*Bridge, *Effects, *Sound, *Manager 등'],
                  ['통지 채널', 'C# event Action<>']],
                 [4.2 * cm, 12.4 * cm]))
story.append(Spacer(1, 6))
story.append(P('4.3 구현 분석', h2))
story.append(P('모듈은 피해를 받으면 상태 변화 시 이벤트를 발신할 뿐, 구독자를 알지 못한다.'))
story.append(codeblk('// ModuleDamageController.TakeDamage()\nOnHit?.Invoke(this, type);                       // 피격\n...\nif (prevState != nextState)\n    OnStateChanged?.Invoke(this, prevState, nextState);  // 상태 변화\nOnDamaged?.Invoke(this, dmg, type);              // 피해량'))
story.append(P('실측 결과 OnStateChanged <b>하나를 6개 독립 시스템이 구독</b>한다: GunDisableBridge(사격), ReloadDisableBridge(장전), TankMobilityBridge(기동), TankCrewManagerBase(승무원), TankEffectsManager·PlayerTankSoundController(연출). 각 구독자는 동일한 생명주기(Start에서 +=, OnDestroy에서 -=)를 따른다.'))
story.append(P('4.4 적용 효과', h2))
story.append(P('· 발신자가 구독자를 전혀 알지 못하므로 <b>완전한 디커플링</b>을 달성한다.<br/>· 새로운 반응 시스템 추가 시 모듈 코드는 불변이다(개방-폐쇄 원칙).'))
story.append(P('4.5 트레이드오프 및 한계 (고찰)', h2))
story.append(P('· <b>이름 vs 실체</b> : *Bridge는 GoF Bridge가 아니라 Observer 구독자 + Adapter에 가깝다. 명명이 설계 의도를 오도한다.<br/>· <b>정적 이벤트의 위험</b> : BallisticManager의 static event는 인스턴스 수명과 무관해, 구독 해제를 누락하면 씬 전환 시 누수로 이어진다. 인스턴스 event와의 대조가 분명하다.<br/>· <b>추적성 저하</b> : 호출 경로가 런타임에 결정되어 디버깅 비용이 증가한다.'))

# ===================== 5. Object Pool =====================
story.append(P('5. 패턴 적용 ② Object Pool + Budget — 자원 관리', h1))
story.append(P('5.1 문제 정의 (Before)', h2))
story.append(P('포탄·폭발·피격 데칼·잔해 화재 등은 전투 중 대량으로 생성·소멸한다. Instantiate / Destroy를 직접 호출하면 GC 스파이크와 드로우콜 누적으로 성능이 저하된다.'))
story.append(P('5.2 패턴 구조', h2))
story.append(tbl([['역할', '구현'],
                  ['Pool (Singleton)', 'PoolManager'],
                  ['Budget / Cull 레이어', 'DecalBudgetManager, WreckEffectManager'],
                  ['Client', 'BallisticManager 등']],
                 [4.6 * cm, 12.0 * cm]))
story.append(Spacer(1, 6))
story.append(P('5.3 구현 분석', h2))
story.append(P('풀은 두 개의 사전으로 구성된다.'))
story.append(codeblk('Dictionary<GameObject, Queue<GameObject>> prefabToPool;   // 프리팹별 풀\nDictionary<GameObject, GameObject> instanceToPrefab;      // 인스턴스 → 프리팹 역매핑'))
story.append(P('역매핑 덕분에 호출부는 원소속 풀을 기억할 필요 없이 Return(instance)만 호출한다. 풀 위에는 예산 레이어가 얹혀, 동시 활성 수가 상한을 넘으면 가장 오래된 객체를 회수(LRU)하고, 주기적으로 거리·줌 기준 컬링을 수행한다.'))
story.append(codeblk('// DecalBudgetManager.Register()\nactiveDecals.Add(...);\nwhile (activeDecals.Count > maxConcurrentDecals)\n    EvictOldest();   // 초과분 풀로 반환'))
story.append(P('5.4 적용 효과', h2))
story.append(P('· 단일 타입이 아닌 <b>N-프리팹 범용 풀</b>로 재사용을 일원화한다.<br/>· 재사용(Pool) → 동시 수 상한(Budget) → 가시성 컬링의 <b>3중 자원 관리</b>를 구성한다.'))
story.append(P('5.5 트레이드오프 및 한계 (고찰)', h2))
story.append(P('· <b>분류 문제</b> : Object Pool은 GoF 23개에 속하지 않는다(Nystrom 분류). 출처 명시가 필요하다.<br/>· <b>중복</b> : DecalBudgetManager와 WreckEffectManager가 거의 동일 구조로, 제네릭 BudgetCullingManager&lt;T&gt;로 통합하는 것이 다음 과제이다. 패턴을 적용했으나 일반화는 미완인 정직한 사례.<br/>· <b>타입 안전성 부재</b> : 범용 GameObject 기반이라 사용처에서 GetComponent가 필요하며, instanceToPrefab에 인스턴스가 누적되어 정리 API가 없다.'))

# ===================== 6. Command =====================
story.append(P('6. 패턴 적용 ③ Command — STT 음성 명령', h1))
story.append(P('6.1 문제 정의', h2))
story.append(P('STT로 인식한 자연어 명령("포수 철갑탄 장전")을 승무원 행동으로 변환해야 한다. 인식 시점과 실행 시점이 다르고, 한 문장에 여러 역할의 명령이 섞일 수 있다.'))
story.append(P('6.2 현행 구조 (부분 적용)', h2))
story.append(codeblk('STT → CrewParser.Parse → Dictionary<CrewRole, List<ParsedCmd>>\n    → Dispatcher: 역할별 Queue<ParsedCmd>\n    → Update(): Dequeue → switch 디스패치 → Receiver 호출'))
story.append(tbl([['Command 역할', '현행', '상태'],
                  ['Command 객체', 'ParsedCmd (데이터 struct)', '× 미객체화'],
                  ['Invoker', 'Queue + Update', '○ 적용'],
                  ['Receiver', 'GunnerController / LoaderController', '○ 적용']],
                 [4.0 * cm, 8.4 * cm, 4.2 * cm]))
story.append(Spacer(1, 6))
story.append(P('6.3 리팩토링 방향 (After)', h2))
story.append(P('ParsedCmd(데이터)를 ICrewCommand(행동) 객체로 승격한다.'))
story.append(codeblk('public interface ICrewCommand { void Execute(); void Undo(); }\n\npublic class MacroCommand : ICrewCommand   // "철갑 장전하고 조준" = 복합 명령\n{ /* Composite */ }'))
story.append(P('팩토리가 ParsedCmd → ICrewCommand를 생성하고, Invoker는 Undo 스택을 갖춘다.'))
story.append(P('6.4 적용 효과 및 한계 (고찰)', h2))
story.append(P('· <b>이미 달성</b> : 큐잉으로 인식·실행 시점이 분리되어 있다(Command의 지연 실행 효용).<br/>· <b>개선 효과</b> : 객체화 시 Undo·매크로·입력 로깅이 가능해진다.<br/>· <b>한계</b> : enum→객체 분기는 팩토리로 이동할 뿐 사라지지 않는다. Fire/Load처럼 되돌릴 수 없는 명령의 Undo는 허상이므로 IUndoableCommand 분리가 필요하다. 명령 수가 적은 현 규모에선 ROI가 제한적이다.'))

# ===================== 7. 고찰 =====================
story.append(P('7. 적용 결과 및 고찰', h1))
story.append(P('7.1 공통 교훈', h2))
story.append(P('패턴은 <b>분기를 제거하는</b> 도구가 아니라 <b>변경의 축을 한 곳으로 모으는</b> 도구다. 프로젝트 규모가 작으면 클래스 증가·가시성 저하·보일러플레이트 등의 비용이 효용을 초과할 수 있으며, 정당화의 기준은 "그 축으로 확장할 일이 있는가"이다.'))
story.append(P('7.2 패턴별 한계 종합', h2))
story.append(tbl([['패턴', '핵심 한계'],
                  ['Observer', '정적 이벤트 누수, 흐름 추적성 저하, 명명-실체 불일치'],
                  ['Object Pool', 'GoF 외 분류, 매니저 중복, 타입 안전성·메모리'],
                  ['Command', 'switch 이동, 비가역 Undo 허상, 소규모 ROI']],
                 [4.0 * cm, 12.6 * cm]))
story.append(Spacer(1, 6))
story.append(P('7.3 향후 개선 방향', h2))
story.append(P('정적 이벤트의 이벤트 버스화, BudgetCullingManager&lt;T&gt; 제네릭 통합, ICrewCommand 완전 객체화.'))

# ===================== 8. 결론 =====================
story.append(P('8. 결론', h1))
story.append(P('세 패턴 모두 결합도 완화라는 본래 목적을 달성했으나, 동시에 추적성·중복·허상 기능 등 현실적 비용을 드러냈다. 패턴 적용의 성패는 패턴 자체가 아니라 <b>적용 맥락과 확장 전망</b>에 달려 있음을 확인하였다.'))
story.append(P('참고문헌', h2))
story.append(P('· E. Gamma et al., <i>Design Patterns: Elements of Reusable Object-Oriented Software</i>, 1994.<br/>· R. Nystrom, <i>Game Programming Patterns</i>, 2014.'))

doc = SimpleDocTemplate(OUT, pagesize=A4, topMargin=2 * cm, bottomMargin=2 * cm,
                        leftMargin=2.2 * cm, rightMargin=2.2 * cm,
                        title='게임 프로그래밍 패턴 적용 리팩토링 보고서')
doc.build(story, onFirstPage=footer, onLaterPages=footer)
print('OK ->', OUT)
print('exists:', os.path.exists(OUT), 'size:', os.path.getsize(OUT) if os.path.exists(OUT) else 0)
