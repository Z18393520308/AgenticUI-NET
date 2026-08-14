import { useMemo, useState } from "react";
import {
  ArrowRight,
  BoundingBox,
  CalendarBlank,
  CaretDown,
  Check,
  CheckCircle,
  FileText,
  GithubLogo,
  LinkSimple,
  List,
  LockKey,
  Minus,
  ShieldCheck,
  Square,
  X,
} from "@phosphor-icons/react";

const githubUrl = "https://github.com/Z18393520308/AgenticUI-NET";

const initialOrder = {
  orderNo: "SO-20260803-001",
  customerType: "企业客户",
  customerName: "华东科技有限公司",
  orderDate: "2026-08-03",
  quantity: "120",
  priority: "普通",
  invoice: true,
};

const steps = [
  { label: "设置订单编号", id: "order.number", field: "orderNo" },
  { label: "选择客户类型", id: "customer.type", field: "customerType" },
  { label: "输入客户名称", id: "customer.name", field: "customerName" },
  { label: "设置订单数量", id: "order.quantity", field: "quantity" },
  { label: "提交订单", id: "order.submit", field: "submit" },
];

const integrations = [
  {
    number: "1",
    title: "安装 NuGet 包",
    copy: "根据界面框架安装 WPF 或 WinForms 包，再按需添加本机网关。",
    code: [
      ["comment", "# WPF\n"],
      ["plain", "dotnet add package "], ["keyword", "AgenticUI.Wpf"],
      ["plain", " --version "], ["value", "0.2.1"],
      ["comment", "\n\n# WinForms\n"],
      ["plain", "dotnet add package "], ["keyword", "AgenticUI.WinForms"],
      ["plain", " --version "], ["value", "0.2.1"],
    ],
  },
  {
    number: "2",
    title: "为控件添加标识",
    copy: "使用替换式控件，或给现有 WPF / WinForms 控件直接绑定。",
    code: [
      ["comment", "// WPF 附加属性\n"],
      ["plain", "AgenticProperties.SetId(\n  customerType, "],
      ["string", "\"customer.type\""], ["plain", ");\n\n"],
      ["comment", "// WinForms Binder\n"],
      ["plain", "AgenticControlBinder.Attach(\n  customerType, options);"],
    ],
  },
  {
    number: "3",
    title: "AI 通过本机通道触发",
    copy: "通过已认证的本机命名管道执行语义命令，并记录结果。",
    code: [
      ["plain", "{\n  "], ["key", "\"action\""], ["plain", ": "],
      ["string", "\"selectItem\""], ["plain", ",\n  "],
      ["key", "\"controlId\""], ["plain", ": "],
      ["string", "\"customer.type\""], ["plain", ",\n  "],
      ["key", "\"value\""], ["plain", ": "],
      ["string", "\"企业客户\""], ["plain", "\n}"],
    ],
  },
];

function Logo() {
  return (
    <a className="brand" href="#top" aria-label="AgenticUI.NET 首页">
      <span className="brand-mark"><BoundingBox size={25} weight="bold" /></span>
      <span>AgenticUI.NET</span>
    </a>
  );
}

function CodeBlock({ tokens }) {
  return (
    <pre className="code-block" aria-label="接入代码示例">
      <code>{tokens.map(([type, text], index) => (
        <span className={`token-${type}`} key={`${type}-${index}`}>{text}</span>
      ))}</code>
    </pre>
  );
}

function AppWindow({ order, setOrder, activeStep, setActiveStep, submitOrder }) {
  const update = (field, value, step) => {
    setOrder((current) => ({ ...current, [field]: value }));
    setActiveStep(step);
  };

  return (
    <section className="desktop-window" aria-label="可交互的订单录入演示">
      <div className="window-titlebar">
        <span>订单录入</span>
        <div className="window-actions" aria-hidden="true">
          <Minus size={14} /><Square size={12} /><X size={14} />
        </div>
      </div>
      <form className="order-form" onSubmit={(event) => { event.preventDefault(); submitOrder(); }}>
        <label>
          <span>订单编号</span>
          <input
            value={order.orderNo}
            onFocus={() => setActiveStep(1)}
            onChange={(event) => update("orderNo", event.target.value, 1)}
          />
        </label>
        <label className={`guided-field ${activeStep === 2 ? "is-active" : ""}`}>
          <span>客户类型</span>
          <span className="select-wrap">
            <select
              value={order.customerType}
              onFocus={() => setActiveStep(2)}
              onChange={(event) => update("customerType", event.target.value, 2)}
            >
              <option>企业客户</option>
              <option>个人客户</option>
              <option>政府客户</option>
            </select>
            <CaretDown size={14} weight="bold" />
          </span>
          <span className="guide-number">2</span>
          <span className="guide-tip">选择客户类型</span>
        </label>
        <label>
          <span>客户名称</span>
          <input
            value={order.customerName}
            onFocus={() => setActiveStep(3)}
            onChange={(event) => update("customerName", event.target.value, 3)}
          />
        </label>
        <label>
          <span>订单日期</span>
          <span className="date-wrap">
            <input
              value={order.orderDate}
              onFocus={() => setActiveStep(1)}
              onChange={(event) => update("orderDate", event.target.value, 1)}
            />
            <CalendarBlank size={15} />
          </span>
        </label>
        <label>
          <span>数量</span>
          <input
            type="number"
            min="1"
            value={order.quantity}
            onFocus={() => setActiveStep(4)}
            onChange={(event) => update("quantity", event.target.value, 4)}
          />
        </label>
        <label>
          <span>优先级</span>
          <span className="select-wrap">
            <select
              value={order.priority}
              onFocus={() => setActiveStep(4)}
              onChange={(event) => update("priority", event.target.value, 4)}
            >
              <option>普通</option><option>紧急</option><option>低</option>
            </select>
            <CaretDown size={14} weight="bold" />
          </span>
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={order.invoice}
            onChange={(event) => update("invoice", event.target.checked, 4)}
          />
          <span>需要发票</span>
        </label>
        <button className="window-submit" type="submit" onFocus={() => setActiveStep(5)}>提交订单</button>
      </form>
    </section>
  );
}

function Timeline({ order, activeStep, submitted }) {
  const values = {
    orderNo: order.orderNo,
    customerType: order.customerType,
    customerName: order.customerName,
    quantity: order.quantity,
    submit: submitted ? "succeeded" : "click",
  };

  return (
    <section className="timeline" aria-live="polite" aria-label="AI 执行动作时间线">
      <div className="timeline-title">
        <span>AI 执行动作时间线</span>
        <span className="live-dot"><i /> 本机</span>
      </div>
      <ol>
        {steps.map((step, index) => {
          const number = index + 1;
          const completed = submitted || number < 5;
          const current = activeStep === number && !submitted;
          return (
            <li className={current ? "is-current" : ""} key={step.id}>
              <span className={`step-index ${completed ? "is-complete" : ""}`}>{number}</span>
              <div>
                <div className="step-heading">
                  <strong>{step.label}</strong>
                  {completed ? <CheckCircle size={17} weight="fill" /> : <span>待执行</span>}
                </div>
                <code>id: {step.id}</code>
                <code>{step.field === "submit" ? "action" : "value"}: {String(values[step.field])}</code>
                <small>10:15:{String(21 + index).padStart(2, "0")}</small>
              </div>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

export function App() {
  const [order, setOrder] = useState(initialOrder);
  const [activeStep, setActiveStep] = useState(2);
  const [submitted, setSubmitted] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  const statusText = useMemo(
    () => submitted ? `订单 ${order.orderNo} 已通过语义动作提交` : null,
    [submitted, order.orderNo],
  );

  const chooseStep = (step) => {
    setActiveStep(step);
    setSubmitted(false);
  };

  const submitOrder = () => {
    setActiveStep(5);
    setSubmitted(true);
  };

  return (
    <div id="top" className="site-shell">
      <header className="site-header">
        <Logo />
        <button className="menu-button" onClick={() => setMenuOpen((open) => !open)} aria-label="切换导航">
          {menuOpen ? <X size={24} /> : <List size={24} />}
        </button>
        <nav className={menuOpen ? "is-open" : ""}>
          <a href="#capabilities" onClick={() => setMenuOpen(false)}>产品能力</a>
          <a href="#integration" onClick={() => setMenuOpen(false)}>接入指南</a>
          <a href="#components" onClick={() => setMenuOpen(false)}>组件</a>
          <a href={githubUrl} target="_blank" rel="noreferrer">GitHub</a>
          <a className="nav-cta" href={githubUrl} target="_blank" rel="noreferrer">开源项目</a>
        </nav>
      </header>

      <main>
        <section className="hero">
          <div className="hero-copy">
            <p className="eyebrow">AI-NATIVE UI FOR .NET DESKTOP</p>
            <h1>无需重写，<br />让现有桌面软件接入 AI</h1>
            <p className="hero-lead">AgenticUI.NET 为 WPF 与 WinForms 控件增加稳定标识、语义事件、可视化引导和本机安全触发。</p>
            <div className="hero-actions">
              <a className="primary-button" href="#integration">开始接入 <ArrowRight size={18} weight="bold" /></a>
              <a className="text-link" href={githubUrl} target="_blank" rel="noreferrer">
                <GithubLogo size={20} weight="fill" /> 查看 GitHub <ArrowRight size={17} />
              </a>
            </div>
            <p className="compatibility"><CheckCircle size={18} weight="fill" /> 支持 .NET 8 与 .NET Framework 4.8</p>
          </div>

          <div className="hero-demo">
            <AppWindow
              order={order}
              setOrder={setOrder}
              activeStep={activeStep}
              setActiveStep={chooseStep}
              submitOrder={submitOrder}
            />
            <div className="agent-bridge" aria-hidden="true">
              <span>AgenticUI</span>
              <ArrowRight size={26} weight="bold" />
            </div>
            <Timeline order={order} activeStep={activeStep} submitted={submitted} />
          </div>
          {statusText && (
            <button className="success-toast" onClick={() => setSubmitted(false)}>
              <Check size={18} weight="bold" /> {statusText} <X size={15} />
            </button>
          )}
        </section>

        <section id="capabilities" className="proof-strip">
          <article><span className="proof-icon"><LinkSimple size={27} /></span><div><h2>原生控件可直接绑定</h2><p>无需重写界面，为现有控件补充稳定 ID 与 AI 语义。</p></div></article>
          <article><span className="proof-icon"><FileText size={27} /></span><div><h2>语义日志默认本地保存</h2><p>记录事件顺序和触发来源，敏感内容可配置脱敏。</p></div></article>
          <article><span className="proof-icon"><ShieldCheck size={27} /></span><div><h2>本机命名管道与令牌认证</h2><p>首版不开放 TCP，使用本机命名管道传输语义命令。</p></div></article>
        </section>

        <section id="integration" className="integration-section">
          <div className="section-heading">
            <p className="eyebrow">渐进式接入</p>
            <h2>三步接入现有项目</h2>
            <p>从一个控件开始，逐步让 AI 安全、可追溯地操作桌面界面。</p>
          </div>
          <div className="integration-grid">
            {integrations.map((item, index) => (
              <article className="integration-card" key={item.number}>
                <span className="card-number">{item.number}</span>
                <h3>{item.title}</h3>
                <p>{item.copy}</p>
                <CodeBlock tokens={item.code} />
                {index < integrations.length - 1 && <ArrowRight className="step-arrow" size={24} />}
              </article>
            ))}
          </div>
          <a
            className="guide-link"
            href={`${githubUrl}/blob/main/docs/quickstart.zh-CN.md`}
            target="_blank"
            rel="noreferrer"
          >查看完整接入指南 <ArrowRight size={18} /></a>
        </section>

        <section id="components" className="component-section">
          <div>
            <p className="eyebrow">WPF + WINFORMS</p>
            <h2>从基础输入到中型数据控件</h2>
          </div>
          <p>统一的控件描述、动作与状态模型，同时支持替换式控件和原生控件绑定。</p>
          <div className="component-list" aria-label="已支持的控件">
            {[
              "Button", "TextBox", "ComboBox", "CheckBox", "RadioButton",
              "DateTimePicker", "NumericUpDown", "ListBox", "TabControl", "DataGrid",
            ].map((name) => <span key={name}>{name}</span>)}
          </div>
        </section>
      </main>

      <footer>
        <Logo />
        <p>面向 AI Agent 的 .NET 桌面 UI 协议与控件库。</p>
        <a href={`${githubUrl}/blob/main/EDITIONS.md`} target="_blank" rel="noreferrer">社区版 / 企业服务</a>
        <a href={githubUrl} target="_blank" rel="noreferrer"><GithubLogo size={20} /> GitHub</a>
      </footer>
    </div>
  );
}
