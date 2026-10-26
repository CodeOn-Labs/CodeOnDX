<script setup lang="ts">
// Hiển thị Markdown tối giản (đoạn, danh sách, **đậm**, `code`) bằng thẻ Vue — không dùng v-html.
import { computed } from 'vue';
import { parseMarkdownLite } from '@/modules/ai/utils/markdown-lite';

const props = defineProps<{ source: string }>();
const blocks = computed(() => parseMarkdownLite(props.source));
</script>

<template>
  <template v-for="(block, bi) in blocks" :key="bi">
    <p v-if="block.kind === 'paragraph'">
      <template v-for="(line, li) in block.lines" :key="li">
        <br v-if="li > 0" />
        <template v-for="(node, ni) in line" :key="ni">
          <strong v-if="node.kind === 'bold'" class="font-semibold">{{ node.text }}</strong>
          <code v-else-if="node.kind === 'code'" class="rounded bg-bg2 px-1 font-mono text-[0.92em]">{{ node.text }}</code>
          <template v-else>{{ node.text }}</template>
        </template>
      </template>
    </p>
    <ol v-else-if="block.ordered" class="list-decimal pl-5 space-y-0.5">
      <li v-for="(item, ii) in block.items" :key="ii">
        <template v-for="(node, ni) in item" :key="ni">
          <strong v-if="node.kind === 'bold'" class="font-semibold">{{ node.text }}</strong>
          <code v-else-if="node.kind === 'code'" class="rounded bg-bg2 px-1 font-mono text-[0.92em]">{{ node.text }}</code>
          <template v-else>{{ node.text }}</template>
        </template>
      </li>
    </ol>
    <ul v-else class="list-disc pl-5 space-y-0.5">
      <li v-for="(item, ii) in block.items" :key="ii">
        <template v-for="(node, ni) in item" :key="ni">
          <strong v-if="node.kind === 'bold'" class="font-semibold">{{ node.text }}</strong>
          <code v-else-if="node.kind === 'code'" class="rounded bg-bg2 px-1 font-mono text-[0.92em]">{{ node.text }}</code>
          <template v-else>{{ node.text }}</template>
        </template>
      </li>
    </ul>
  </template>
</template>
