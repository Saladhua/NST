// 系统设置页（管理员）：展示配置项列表，逐行编辑后点击「保存」立即生效。
import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Form, Input, Spin, Table, Typography } from 'antd';
import { SaveOutlined } from '@ant-design/icons';
import { configApi } from '../../api/config';
import type { ConfigItemDto } from '../../types';

export default function SettingsPage() {
  const { message } = App.useApp();
  const [configs, setConfigs] = useState<ConfigItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});

  // 加载全部配置项并初始化编辑表单值
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await configApi.list();
      setConfigs(result);
      const init: Record<string, string> = {};
      result.forEach((item) => {
        init[item.id] = item.configValue;
      });
      setValues(init);
    } catch {
      message.error('加载配置失败');
    } finally {
      setLoading(false);
    }
  }, [message]);

  useEffect(() => {
    void load();
  }, [load]);

  // 保存单个配置项
  const save = async (item: ConfigItemDto) => {
    setSavingKey(item.id);
    try {
      await configApi.update(item.id, values[item.id] ?? '');
      message.success('保存成功');
      void load();
    } catch {
      message.error('保存失败');
    } finally {
      setSavingKey(null);
    }
  };

  const columns = [
    { title: '配置项', dataIndex: 'configKey', key: 'configKey', width: 220 },
    {
      title: '说明',
      dataIndex: 'description',
      key: 'description',
      width: 220,
      render: (v: string | null) => v ?? '-',
    },
    {
      title: '配置值',
      key: 'value',
      render: (_: unknown, record: ConfigItemDto) => (
        <Input
          value={values[record.id] ?? ''}
          onChange={(e) => setValues((prev) => ({ ...prev, [record.id]: e.target.value }))}
        />
      ),
    },
    {
      title: '更新时间',
      dataIndex: 'updatedAt',
      key: 'updatedAt',
      width: 160,
      render: (v: string | null) =>
        v ? new Date(v).toLocaleString('zh-CN') : '从未更新',
    },
    {
      title: '操作',
      key: 'action',
      width: 90,
      render: (_: unknown, record: ConfigItemDto) => (
        <Button
          type="link"
          icon={<SaveOutlined />}
          loading={savingKey === record.id}
          onClick={() => void save(record)}
        >
          保存
        </Button>
      ),
    },
  ];

  if (loading) {
    return (
      <Card className="flex items-center justify-center py-20">
        <Spin size="large" />
      </Card>
    );
  }

  return (
    <Card title="系统设置">
      <Typography.Paragraph type="secondary" className="mb-4">
        修改后点击对应行的「保存」立即生效。
      </Typography.Paragraph>
      <Form layout="vertical">
        <Table<ConfigItemDto>
          rowKey="id"
          columns={columns}
          dataSource={configs}
          pagination={false}
          locale={{ emptyText: '暂无配置项' }}
        />
      </Form>
    </Card>
  );
}