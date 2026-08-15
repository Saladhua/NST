// 前端应用入口：配置 antd 中文语言包、挂载消息桥与根组件。
import React from 'react';
import ReactDOM from 'react-dom/client';
import { App as AntdApp, ConfigProvider } from 'antd';
import zhCN from 'antd/locale/zh_CN';
import dayjs from 'dayjs';
import 'dayjs/locale/zh-cn';
import App from './App';
import { MessageBridge } from './utils/message-holder';
import './styles/index.css';

// 日期库使用中文语言
dayjs.locale('zh-cn');

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={zhCN}>
      <AntdApp>
        {/* MessageBridge 将 antd 消息实例提升到模块级，供 http 拦截器使用 */}
        <MessageBridge />
        <App />
      </AntdApp>
    </ConfigProvider>
  </React.StrictMode>,
);