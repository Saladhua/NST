// 平台支持的客户白名单：客户下单对应图号.xlsx 的基础资料里只展示这几家。
// 仅用于页面展示过滤，不影响数据导入与匹配逻辑。
const SUPPORTED_CUSTOMER_KEYWORDS = ['创达', '华尔达', '三可', '发润达', '法拉达', '马鞍山仪达'];

/** 是否属于平台支持的客户（按名称包含判断，兼容「发润达 / 法拉达」两种叫法）。 */
export function isSupportedCustomer(name: string | null | undefined): boolean {
  if (!name) {
    return false;
  }
  return SUPPORTED_CUSTOMER_KEYWORDS.some((keyword) => name.includes(keyword));
}
