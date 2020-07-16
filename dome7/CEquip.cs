using AlarmCenter.DataCenter;
using System;
using System.Data;
using System.Threading.Tasks;

namespace dome7
{
    public class CEquip : CEquipBase
    {
        public Client client = new Client();
        bool _bInit = false;//是否初始化
        int _sleepTime = 300;
        public override CommunicationState GetData(CEquipBase pEquip)
        {
            if (RunSetParmFlag)
            {
                return CommunicationState.setreturn;
            }
            Sleep(_sleepTime);
            return base.GetData(pEquip);
        }

        public override bool GetYC(DataRow r)
        {
            try
            {
                SetYCData(r, client.Message);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool GetYX(DataRow r)
        {
            return base.GetYX(r);
        }

        public override bool init(EquipItem item)
        {
            if (_bInit == false || ResetFlag)
            {
                if (base.init(item) == false)
                {
                    return false;//通讯失败
                }
                try
                {
                    //获取Equip表communication_time_param中的延迟值
                    _sleepTime = Convert.ToInt32(item.communication_time_param);
                }
                catch
                {
                    //获取失败或信息不正确则默认300，这是比较好的异常处理机制：含修正代码
                    _sleepTime = 300;
                }

                Task.Run(async () => { await client.ConnectMqttServerAsync(); });
                _bInit = true;
            }
            return true;
        }

        public override bool SetParm(string MainInstruct, string MinorInstruct, string Value)
        {
            return base.SetParm(MainInstruct, MinorInstruct, Value);
        }
    }
}
