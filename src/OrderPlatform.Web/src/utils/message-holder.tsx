// 消息桥：将 antd 的 message 实例提升到模块级。
// 由于 axios 拦截器在 React 组件外运行，无法直接使用 App.useApp()，
// 通过此桥接组件在应用挂载时保存实例，供拦截器调用提示。
import { useEffect } from 'react';
import { App } from 'antd';

type MessageInstance = ReturnType<typeof App.useApp>['message'];

let messageInstance: MessageInstance | null = null;

function setMessageInstance(instance: MessageInstance): void {
  messageInstance = instance;
}

/** 获取全局 message 实例（可能为 null）。 */
export function getMessageInstance(): MessageInstance | null {
  return messageInstance;
}

/** 桥接组件：渲染时保存 message 实例，不产生任何 UI。 */
export function MessageBridge(): null {
  const { message } = App.useApp();

  useEffect(() => {
    setMessageInstance(message);
  }, [message]);

  return null;
}