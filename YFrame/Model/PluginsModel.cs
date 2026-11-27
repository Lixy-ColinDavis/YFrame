using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 插件-数据类型
    /// </summary>
    public class PluginsModel : ViewModelBase
    {
        /// <summary>缓存的图标背景画刷，避免每次绑定都重新创建</summary>
        private Brush? _iconBrush;

        /// <summary>
        /// 图标缩写：去掉 YF_ 前缀后取前两个字符，供侧边栏方形图标块显示
        /// </summary>
        public string IconText => PluginIconHelper.GetText(ID, Name);

        /// <summary>
        /// 图标背景：按 ID 稳定哈希从配色表取一组渐变色，目标已冻结
        /// </summary>
        public Brush IconBrush => _iconBrush ??= PluginIconHelper.GetBrush(ID, Name);

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;

                _name = value;
                // 名称参与图标缩写/配色回退，先通知图标，再通知 Name 本身，
                // 保证外部监听者最后收到的是 Name 变更（与既有测试契约一致）
                OnPropertyChanged(nameof(IconText));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(Name));
            }
        }

        private string _id = string.Empty;
        public string ID
        {
            get => _id;
            set
            {
                if (_id == value) return;

                _id = value;
                // ID 变化会影响图标缩写与配色，先刷新图标，最后通知 ID
                _iconBrush = null;
                OnPropertyChanged(nameof(IconText));
                OnPropertyChanged(nameof(IconBrush));
                OnPropertyChanged(nameof(ID));
            }
        }

        /// <summary>
        /// 状态  0: 关闭, 1:显示, 2:驻留
        /// </summary>
        private int _status;
        public int Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }
}
