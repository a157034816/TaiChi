/**
 * 表示视觉编程 playground 的种子输入。
 * 该类型用于把“起始参数”从源节点传递到后续图层生成节点。
 */
export class GeneratorSeed {
  constructor({ seedId = "", prompt = "" } = {}) {
    this.seedId = seedId;
    this.prompt = prompt;
  }
}

/**
 * 表示图层分发阶段生成的中间信号。
 * 该契约刻意保持中性，方便 Demo 用于任意视觉合成场景。
 */
export class LayerSignal {
  constructor({ channel = "", profile = "" } = {}) {
    this.channel = channel;
    this.profile = profile;
  }
}

/**
 * 表示预览阶段消费的帧结果。
 * 同一个 frame 可以对应主输出或变体输出。
 */
export class PreviewFrame {
  constructor({ frameId = "", variant = "" } = {}) {
    this.frameId = frameId;
    this.variant = variant;
  }
}
