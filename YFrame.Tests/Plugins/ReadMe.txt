//***********************************************************************




YF_AIHelper	核心是 LLamaSharp 模型推理，需要 4GB+ GGUF 模型文件，无法在测试中加载
YF_Clicker	核心逻辑中点击依赖 InputSimulator，需要真实鼠标
YF_HttpServer	核心是 HttpListener 监听端口，依赖网络栈，属于集成测试范畴
YF_Penetration	核心是 P2P NAT 穿透，依赖真实网络和自研 NatTraversal 库
YF_ScreenOCRTranslate	核心是 PaddleOCR + 百度翻译 API，依赖模型文件 + 网络

************************************************************************/