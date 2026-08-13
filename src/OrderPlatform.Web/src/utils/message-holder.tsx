import { useEffect } from 'react';
import { App } from 'antd';

type MessageInstance = ReturnType<typeof App.useApp>['message'];

let messageInstance: MessageInstance | null = null;

function setMessageInstance(instance: MessageInstance): void {
  messageInstance = instance;
}

export function getMessageInstance(): MessageInstance | null {
  return messageInstance;
}

export function MessageBridge(): null {
  const { message } = App.useApp();

  useEffect(() => {
    setMessageInstance(message);
  }, [message]);

  return null;
}