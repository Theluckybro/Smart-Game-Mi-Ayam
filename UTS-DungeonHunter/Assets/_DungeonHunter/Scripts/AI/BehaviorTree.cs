using System;
using System.Collections.Generic;

namespace DungeonHunter.BT
{
    public enum BTStatus
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// Konteks satu kali Tick. Menyimpan jalur node yang sedang aktif
    /// (contoh: "Root > Ground Slam > Slam!") untuk ditampilkan di AI Debug Mode.
    /// </summary>
    public class BTContext
    {
        private readonly List<string> stack = new List<string>();

        public string ActivePath { get; private set; } = "-";
        public BTStatus LastStatus { get; private set; }

        internal void Push(string name) => stack.Add(name);
        internal void Pop() => stack.RemoveAt(stack.Count - 1);

        internal void MarkLeaf(BTStatus status)
        {
            ActivePath = string.Join(" > ", stack);
            LastStatus = status;
        }

        internal void Reset() => stack.Clear();
    }

    public abstract class BTNode
    {
        public string Name { get; }

        protected BTNode(string name)
        {
            Name = name;
        }

        public BTStatus Tick(BTContext ctx)
        {
            ctx.Push(Name);
            BTStatus status = OnTick(ctx);
            ctx.Pop();
            return status;
        }

        protected abstract BTStatus OnTick(BTContext ctx);
    }

    /// <summary>Selector (fallback): coba child berurutan, berhenti di child pertama yang tidak Failure.</summary>
    public class BTSelector : BTNode
    {
        private readonly BTNode[] children;

        public BTSelector(string name, params BTNode[] children) : base(name)
        {
            this.children = children;
        }

        protected override BTStatus OnTick(BTContext ctx)
        {
            foreach (BTNode child in children)
            {
                BTStatus status = child.Tick(ctx);
                if (status != BTStatus.Failure)
                    return status;
            }
            return BTStatus.Failure;
        }
    }

    /// <summary>Sequence: jalankan child berurutan, berhenti di child pertama yang tidak Success.</summary>
    public class BTSequence : BTNode
    {
        private readonly BTNode[] children;

        public BTSequence(string name, params BTNode[] children) : base(name)
        {
            this.children = children;
        }

        protected override BTStatus OnTick(BTContext ctx)
        {
            foreach (BTNode child in children)
            {
                BTStatus status = child.Tick(ctx);
                if (status != BTStatus.Success)
                    return status;
            }
            return BTStatus.Success;
        }
    }

    /// <summary>Leaf kondisi: Success jika predicate true, selain itu Failure.</summary>
    public class BTCondition : BTNode
    {
        private readonly Func<bool> predicate;

        public BTCondition(string name, Func<bool> predicate) : base(name)
        {
            this.predicate = predicate;
        }

        protected override BTStatus OnTick(BTContext ctx) =>
            predicate() ? BTStatus.Success : BTStatus.Failure;
    }

    /// <summary>Leaf aksi: menjalankan perilaku dan mengembalikan Success / Failure / Running.</summary>
    public class BTAction : BTNode
    {
        private readonly Func<BTStatus> action;

        public BTAction(string name, Func<BTStatus> action) : base(name)
        {
            this.action = action;
        }

        protected override BTStatus OnTick(BTContext ctx)
        {
            BTStatus status = action();
            if (status != BTStatus.Failure)
                ctx.MarkLeaf(status);
            return status;
        }
    }

    /// <summary>Pembungkus tree + konteks.</summary>
    public class BehaviorTree
    {
        private readonly BTNode root;
        public BTContext Context { get; } = new BTContext();

        public BehaviorTree(BTNode root)
        {
            this.root = root;
        }

        public BTStatus Tick()
        {
            Context.Reset();
            return root.Tick(Context);
        }
    }
}
